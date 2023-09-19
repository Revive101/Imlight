/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.Server.Login.Exceptions;

internal enum UserValidateError
{
    NoError = 0,
    AccountBanned = 87620544,
    MachineBanned = 1157331960,
    ValidateFailed = 246825817,
    Timeout = 1361855231,
}