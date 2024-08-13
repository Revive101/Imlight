/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common.Utilities;
using Newtonsoft.Json;
using Imlight.Common.Configuration;
using Imlight.CoreLib.WizardData.Implementations;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;

namespace Imlight.CoreLib.WizardData.Models.Player;

[Flags]
public enum AccountFlags {
    IsHallMonitor = 1 << 0,
    CanChat = 1 << 1,
    CanFilteredChat = 1 << 2,
    CanOpenChat = 1 << 3,
    CanOpenChatLegacy = 1 << 4,
    CanTrueFriendCode = 1 << 5,
    CanGift = 1 << 6,
    CanReportBugs = 1 << 7,
    Unknown1 = 1 << 8,
    Unknown2 = 1 << 9,
    Unknown3 = 1 << 10,
    CanEarnCrownsOffers = 1 << 11,
    CanEarnCrownsButton = 1 << 12,
}

[Serializable]
public enum AuthLevel {
    None,
    QualityAssurance,
    HallMonitor,
    Developer,
    Administrator
}

[Serializable]
public enum ChatMode {
    Open,
    Filtered,
    Closed
}

[Serializable]
public class Account {
    [JsonIgnore] public readonly byte MAX_ALLOWED_CHARACTERS = ConfigurationManager.Settings.MaxAllowedCharactersPerAccount;

    public ulong AccountId { get; private set; }
    public string Username { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; set; }
    public AuthLevel AuthLevel { get; set; }
    public ChatMode ChatMode { get; set; }
    public List<ulong> CharacterIds { get; private set; } = new();
    public List<ulong> InfractionIds { get; private set; } = new();
    public DateTime CreationTime { get; private set; }
    public DateTime LastLoginTime { get; set; }
    public ulong LastLoginMachineId { get; set; }
    public string LastLoginIp { get; set; }
    public bool IsLocked { get; set; }

    [JsonIgnore] public List<Wizard> Characters = new();
    [JsonIgnore] public InfractionHistory InfractionHistory { get; set; }
    [JsonIgnore] public SessionActor SessionActor { get; set; }

    [JsonConstructor] public Account() {  }

    // ctor
    public Account(string username, string email, string passwordHash) {
        if (string.IsNullOrWhiteSpace(username)) {
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordHash)) {
            return;
        }

        this.AccountId = RandomGen.GenerateGUID();
        this.Username = username;
        this.Email = email;
        this.PasswordHash = passwordHash;
        this.CreationTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a character to the account.
    /// </summary>
    /// <param name="character">The character to add.</param>
    /// <returns>True if the character was successfully added, false otherwise.</returns>
    public bool AddCharacter(Wizard character) {
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
        var savedCharacterToCollection = WizardCollection
            .AddCharacter(character);
        var savedCharacterToAccount = AccountCollection
            .AddCharacterToAccount(AccountId, character.CharId);

        if (!savedCharacterToCollection || !savedCharacterToAccount) {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Deletes a character with the specified ID from the account.
    /// </summary>
    /// <param name="id">The ID of the character to delete.</param>
    /// <returns>True if the character was successfully deleted, false otherwise.</returns>
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
    /// Retrieves a character with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the character to retrieve.</param>
    /// <returns>The character with the specified ID.</returns>
    public Wizard GetCharacter(ulong id)
        => this.Characters.First(c => c.CharId == id);

    /// <summary>
    /// Adds an infraction to the account.
    /// </summary>
    /// <param name="infractionType">The type of infraction.</param>
    /// <param name="reason">The logged warning of the infraction.</param>
    /// <param name="source">The source of where this infraction is coming from.</param>
    /// <param name="expiration">When this infraction will expire.</param>
    /// <returns>The infraction given to the account.</returns>
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

    /// <summary>
    /// Removes an infraction from the account.
    /// </summary>
    /// <param name="infractionIndex">The index of the infraction to remove.</param>
    /// <returns>True if the infraction was successfully removed, false otherwise.</returns>
    public bool RemoveInfraction(int infractionIndex) {
        if (infractionIndex < 0 || infractionIndex >= InfractionHistory.Infractions.Count) {
            return false;
        }

        var infraction = InfractionHistory.Infractions[infractionIndex];
        InfractionHistory.Infractions.RemoveAt(infractionIndex);
        InfractionIds.Remove(infraction.InfractionId);

        // Remove the infraction from the database.
        InfractionCollection.RemoveInfraction(infraction.InfractionId);
        AccountCollection.RemoveInfractionFromAccount(this.AccountId, infraction.InfractionId);

        return true;
    }

    /// <summary>
    /// Waives the current ban for the player.
    /// </summary>
    /// <param name="source">The source of the waiver.</param>
    /// <returns>True if the ban was successfully waived, false otherwise.</returns>
    public bool WaiveCurrentBan(string source) {
        var currentBan = InfractionHistory
            .Infractions
            .Where(x => x.InfractionType == InfractionType.Ban && !x.IsExpired && !x.WasWaived)
            .FirstOrDefault();
        if (currentBan is null) {
            return false;
        }

        currentBan.WasWaived = true;
        currentBan.WasWaivedBy = source;

        // Update the infraction in the database.
        InfractionCollection.UpdateInfraction(currentBan);

        return true;
    }

    /// <summary>
    /// Waives the current mute for the player.
    /// </summary>
    /// <param name="source">The source of the waiver.</param>
    /// <returns>True if the current mute was successfully waived, false otherwise.</returns>
    public bool WaiveCurrentMute(string source) {
        var currentMute = InfractionHistory
            .Infractions
            .Where(x => x.InfractionType == InfractionType.Mute && !x.IsExpired && !x.WasWaived)
            .FirstOrDefault();
        if (currentMute is null) {
            return false;
        }

        currentMute.WasWaived = true;
        currentMute.WasWaivedBy = source;

        // Update the infraction in the database.
        InfractionCollection.UpdateInfraction(currentMute);

        return true;
    }

    public AccountFlags GetAccountFlags() {
        AccountFlags flags = 0;

        flags |= AuthLevel >= AuthLevel.HallMonitor ? AccountFlags.IsHallMonitor : 0;
        flags |= AuthLevel >= AuthLevel.QualityAssurance ? AccountFlags.CanReportBugs : 0;

        switch (ChatMode) {
            case ChatMode.Open:
                flags |= AccountFlags.CanOpenChat;
                break;
            case ChatMode.Filtered:
                flags |= AccountFlags.CanFilteredChat;
                break;
            case ChatMode.Closed:
                flags |= AccountFlags.CanChat;
                break;
        }

        // Always true.
        flags |= AccountFlags.CanTrueFriendCode;
        flags |= AccountFlags.CanGift;

        return flags;
    }
}
