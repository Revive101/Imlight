/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Imlight.Common;

namespace Imlight.CoreLib.WizardData.Databases;

public class WorldDatabase : RavenDatabaseSingleton<WorldDatabase> {

    protected override string DatabaseName { get; } 
        = ConfigurationManager.Settings["Database.WorldDatabaseName"];
    protected override string Url { get; } 
        = ConfigurationManager.Settings["Database.WorldDatabaseUrl"];
    protected override string CertificatePath { get; } 
        = ConfigurationManager.Settings["Database.WorldDatabaseCertificatePath"];

}