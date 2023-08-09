/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.WizardData.Collections;
using Newtonsoft.Json;

namespace Imlight.Server.Login.Models
{
    public class Account
    {
        public const byte MAX_ALLOWED_CHARACTERS = 6;

        /// <summary>
        /// The account ID.
        /// </summary>
        public ulong AccountId { get; private set; }

        /// <summary>
        /// The username of the account.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// The email of the account.
        /// </summary>
        public string Email { get; private set; }

        /// <summary>
        /// The ClientKey1 hash of the plaintext password.
        /// </summary>
        public string PasswordHash { get; private set; }

        /// <summary>
        /// The authentication level of the account.
        /// </summary>
        public AuthLevel AuthLevel { get; }

        public readonly List<ulong> CharacterIds = new();

        [JsonIgnore] public List<Character> Characters = new();

        public Account(string username, string email, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(username));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(email));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(passwordHash));
            
            this.AccountId = RandomGen.GenerateGUID();
            this.Username = username;
            this.Email = email;
            this.PasswordHash = passwordHash;
        }

        public Character AddCharacter(Character character)
        {
            // Change the character account ID to this account's ID.
            character.AccountId = this.AccountId;
            
            CharacterCollection.AddCharacter(character);
            this.CharacterIds.Add(character.CharId);
            this.Characters.Add(character);

            return character;
        }

        public Character GetCharacter(ulong id)
        {
            return this.Characters.First(c => c.CharId == id);
        }

        public bool DeleteCharacter(ulong id)
        {
            // Check to see if the character exists with our account id.
            if (!this.CharacterIds.Contains(id))
                return false;
            
            // Delete the character from the database.
            CharacterCollection.DeleteCharacter(id);
            
            Characters.RemoveAll(c => c.CharId == id);
            CharacterIds.Remove(id);

            return true;
        }
    }
}
