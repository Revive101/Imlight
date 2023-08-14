/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common.Utilities;
using Imlight.Server.Game.Models;
using Imlight.Server.WizardData.Implementations;
using Newtonsoft.Json;
using BCrypt.Net;
using Imlight.Common.Cryptography;

namespace Imlight.Server.Login.Models
{
    public class Account
    {
        [JsonIgnore] public const byte MAX_ALLOWED_CHARACTERS = 6;
        
        public ulong AccountId { get; private set; }
        public string Username { get; private set; }
        public string Email { get; private set; }
        public string PasswordHash { get; private set; }
        public AuthLevel AuthLevel { get; init; }
        public List<ulong> CharacterIds { get; private set; } = new();

        [JsonIgnore] public List<Character> Characters = new();
        
        // Empty constructor for deserialization.
        [JsonConstructor] public Account() {}

        public Account(string username, string email, string plaintextPassword)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;
            if (string.IsNullOrWhiteSpace(email))
                return;
            if (string.IsNullOrWhiteSpace(plaintextPassword))
                return;
            
            this.AccountId = RandomGen.GenerateGUID();
            this.Username = username;
            this.Email = email;
            
            this.PasswordHash = CreateHashedPassword(plaintextPassword);
        }

        public bool AddCharacter(Character character)
        {
            // Return false if adding this character would exceed the maximum allowed characters per account.
            // Return false if the character already exists in the account.
            if (this.CharacterIds.Count >= MAX_ALLOWED_CHARACTERS)
                return false;
            if (this.CharacterIds.Contains(character.CharId))
                return false;
            
            // Change the character account ID to this account's ID.
            character.AccountId = this.AccountId;

            this.CharacterIds.Add(character.CharId);
            this.Characters.Add(character);

            return true;
        }

        public bool DeleteCharacter(ulong id)
        {
            // Check to see if the character exists with our account id.
            if (!this.CharacterIds.Contains(id))
                return false;

            Characters.RemoveAll(c => c.CharId == id);
            CharacterIds.Remove(id);

            return true;
        }
        
        public Character GetCharacter(ulong id) 
            => this.Characters.First(c => c.CharId == id);

        private static string CreateHashedPassword(string plaintextPassword)
        {
            using var sha512 = SHA512.Create();
            var passwordBytes = Encoding.UTF8.GetBytes(plaintextPassword);

            return Convert.ToBase64String(sha512.ComputeHash(passwordBytes));
        }
    }
}
