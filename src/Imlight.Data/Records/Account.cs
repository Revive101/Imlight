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
        public AuthLevel AuthLevel { get; private set; }

        /// <summary>
        /// An array of this account's character data.
        /// </summary>
        public List<Character> Characters { get; private set; }

        public Account(string Username, string Email, string Password)
        {
            this.ID = RandomGen.GenerateId();
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
    }
}
