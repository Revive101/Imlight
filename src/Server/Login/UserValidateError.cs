using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WizUnraveler;

namespace Imlight.Server.Login
{
    internal enum UserValidateError
    {
        NoError = 0,
        AccountBanned = 87620544,
        MachineBanned = 1157331960,
        ValidateFailed = 246825817,
        Timeout = 1361855231,
    }
}
