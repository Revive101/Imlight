/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;

namespace Imlight.Director.EmbeddedAccounts;

/// <summary>
/// Represents a hard-coded account that persiss on updates.
/// </summary>
internal abstract class EmbeddedAccount {
    internal string Username { get; init; }
    internal string Password { get; init; }
    internal string Email { get; init; }
    internal AuthLevel AuthLevel { get; init; }
    internal Wizard DefaultWizard { get; private set; }
    protected Account Account { get; init; }

    internal EmbeddedAccount(string Username, string plaintextPassword, string Email, AuthLevel AuthLevel) {
        this.Username = Username;
        this.Password = plaintextPassword;
        this.Email = Email;
        this.AuthLevel = AuthLevel;
        DefaultWizard = CreateDefaultWizard();

        // Create the account in the database. This may return failure, but it's fine.
        Account = DatabaseUtilities.CreateEmbeddedDatabaseAccount(Username, Email, Password, AuthLevel);
        if (Account is null) {
            return;
        }

        Logger.Information("Created embedded account {0}.", Logger.Args(Username));
        _ = Account.AddCharacter(DefaultWizard);
    }

    protected abstract Wizard CreateDefaultWizard();
}
