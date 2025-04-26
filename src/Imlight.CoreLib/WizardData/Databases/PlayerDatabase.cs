/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;

namespace Imlight.CoreLib.WizardData.Databases;

public class PlayerDatabase : RavenDatabaseSingleton<PlayerDatabase> {

    protected override string DatabaseName { get; } 
        = ConfigurationManager.Settings["Database.PlayerDatabaseName"];
    protected override string Url { get; } 
        = ConfigurationManager.Settings["Database.PlayerDatabaseUrl"];
    protected override string CertificatePath { get; } 
        = ConfigurationManager.Settings["Database.PlayerDatabaseCertificatePath"];

}