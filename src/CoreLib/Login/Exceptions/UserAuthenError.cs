/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

namespace Imlight.CoreLib.Login.Exceptions;

public enum UserAuthenError {
    AccountBanned = 0x538FBC0,
    MachineBanned = 0x44FB7BF8,
    AuthenFailed = 0x3B689180,
    AISNoLogin = 0x6311BDD6,
    Timeout = 0x512C42FF,
    FtpCapped = 0x5BFF7366,
    ErrorNoLock = 0x67DD13EA,
    FailedUpload = 0x10857D75
}
