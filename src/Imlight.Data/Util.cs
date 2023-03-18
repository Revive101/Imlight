using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler;
using WizUnraveler.Cache;

namespace Imlight.Data
{
    public static class Util
    {
        private static Account _debugAccount;

        /// <summary>
        /// Creates and returns a debug account.
        /// </summary>
        /// <returns></returns>
        public static Account GetDebugAccount()
        {
            if (_debugAccount is not null)
                return _debugAccount;

            // Create a new debug account.
            _debugAccount = new Account("Chi", "Chi2Chomp@mail.com", "Password");
            _debugAccount.AuthLevel = AuthLevel.Administrator;

            return _debugAccount;
        }
    }
}
