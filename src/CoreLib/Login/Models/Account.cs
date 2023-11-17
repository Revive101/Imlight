/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common.Utilities;
using Imlight.CoreLib.Game.Models;
using Newtonsoft.Json;
using Imlight.Common.Configuration;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Models;
using Imlight.CoreLib.WizardData.Implementations;

namespace Imlight.CoreLib.Login.Models;

[Serializable]
public class Account {
    [JsonIgnore] public readonly byte MAX_ALLOWED_CHARACTERS = ConfigurationManager.Settings.MaxAllowedCharactersPerAccount;

    public ulong AccountId { get; private set; }
    public string Username { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; set; }
    public AuthLevel AuthLevel { get; set; }
    public List<ulong> CharacterIds { get; private set; } = new();
    public List<ulong> InfractionIds { get; private set; } = new();
    public DateTime CreationTime { get; private set; }
    public DateTime LastLoginTime { get; set; }
    public ulong LastLoginMachineId { get; set; }
    public string LastLoginIp { get; set; }
    public bool IsLocked { get; set; }

    [JsonIgnore] public List<Character> Characters = new();
    [JsonIgnore] public InfractionHistory InfractionHistory { get; set; }

    [JsonConstructor] public Account() {  }

    // ctor
    public Account(string username, string email, string plaintextPassword) {
        if (string.IsNullOrWhiteSpace(username)) {
            return;
        }

        if (string.IsNullOrWhiteSpace(plaintextPassword)) {
            return;
        }

        this.AccountId = RandomGen.GenerateGUID();
        this.Username = username;
        this.Email = email;
        this.PasswordHash = DatabaseUtilities.CreateHashedPassword(plaintextPassword);
        this.CreationTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a character to the account.
    /// </summary>
    /// <param name="character"></param>
    /// <returns></returns>
    public bool AddCharacter(Character character) {
        // Return false if adding this character would exceed the maximum allowed characters per account.
        // Return false if the character already exists in the account.
        if (this.CharacterIds.Count >= MAX_ALLOWED_CHARACTERS) {
            return false;
        }

        if (this.CharacterIds.Contains(character.CharId)) {
            return false;
        }

        // Change the character account ID to this account's ID.
        character.AccountId = this.AccountId;

        this.CharacterIds.Add(character.CharId);
        this.Characters.Add(character);

        // Save the character persistently.
        var savedCharacterToCollection = CharacterCollection
            .AddCharacter(character);
        var savedCharacterToAccount = AccountCollection
            .AddCharacterToAccount(AccountId, character.CharId);

        if (!savedCharacterToCollection || !savedCharacterToAccount) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Deletes a character from the account.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool DeleteCharacter(ulong id) {
        // Check to see if the character exists with our account id.
        if (!this.CharacterIds.Contains(id)) {
            return false;
        }

        Characters.RemoveAll(c => c.CharId == id);
        CharacterIds.Remove(id);

        return true;
    }

    /// <summary>
    /// Gets a character from the account.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Character GetCharacter(ulong id)
        => this.Characters.First(c => c.CharId == id);

    public Infraction AddInfraction(InfractionType infractionType, string reason, string source = null, DateTime? expiration = null) {
        var infraction = new Infraction {
            InfractionId = RandomGen.GenerateGUID(),
            AccountId = this.AccountId,
            MachineId = LastLoginMachineId,
            InfractionType = infractionType,
            InfractionTime = DateTime.UtcNow,
            Reason = reason,
            Expiration = expiration,
            ResponsibleModerator = source ?? "Imlight"
        };

        this.InfractionIds.Add(infraction.InfractionId);
        this.InfractionHistory.AddInfraction(infraction);

        // Save the infraction to the database.
        InfractionCollection.AddInfraction(infraction);
        AccountCollection.AddInfractionToAccount(this.AccountId, infraction.InfractionId);

        return infraction;
    }
}
