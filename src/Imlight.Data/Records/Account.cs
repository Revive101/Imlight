using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Imlight.Common;

namespace Imlight.Data
{
    public class Account
    {
        public const byte MAX_ALLOWED_CHARACTERS = 6;

        /// <summary>
        /// The account ID.
        /// </summary>
        public ulong ID { get; private set; }

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
        public AuthLevel AuthLevel { get; set; }

        /// <summary>
        /// An array of this account's character data.
        /// </summary>
        public List<Character> Characters { get; private set; }

        public Account(string Username, string Email, string Password)
        {
            this.ID = RandomGen.GenerateGUID();
            this.Username = Username;
            this.Email = Email;
            this.PasswordHash = Password;
            this.Characters = new List<Character>();
        }

        /// <summary>
        /// Adds a character to this account.
        /// </summary>
        /// <param name="character"></param>
        /// <returns>False, if an error occurs or the account doesn't have anymore character slots.</returns>
        public bool AddCharacter(Character character)
        {
            if (Characters.Count >= MAX_ALLOWED_CHARACTERS)
                return false;

            this.Characters.Add(character);

            return true;
        }

        /// <summary>
        /// Deletes a character from this account.
        /// </summary>
        /// <param name="charId"></param>
        /// <returns>False, if no character by ID is found. Otherwise, true.</returns>
        public bool DeleteCharacter(ulong charId)
        {
            if (Characters.Any(x => x.ID == charId))
            {
                var character = Characters.First(x => x.ID == charId);
                Characters.Remove(character);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to get a character from this account.
        /// </summary>
        /// <param name="charId"></param>
        /// <param name="character"></param>
        /// <returns>True, if the character is found; otherwise, false.</returns>
        public bool GetCharacter(ulong charId, out Character character)
        {
            character = null;

            if (Characters.Any(x => x.ID == charId))
            {
                character = Characters.First(x => x.ID == charId);

                return true;
            }

            return false;
        }
    }
}
