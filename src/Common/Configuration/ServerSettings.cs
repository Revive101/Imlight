/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;

namespace Imlight.Common.Configuration;

[IniSection("Global Settings")]
public sealed class ServerSettings {
    [DefaultValue("r754018.Wizard_1_540")]
    public string GameRevision { get; set; }

    #region Logging

    [IniSection("Logging")]

    [DefaultValue("./logs/log.txt")]
    public string? LogPath { get; set; }

    [DefaultValue("INFO")]
    [Description("The minimum log level to be written to the log file. Valid values are: TRACE, DEBUG, INFO, WARN, ERROR, FATAL")]
    public string LogLevel { get; set; }

    [DefaultValue("{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3} {CallingSpace} : {Message:lj} {NewLine}{Exception}")]
    public string LogFormat { get; set; }

    [DefaultValue("http://localhost:5341")]
    [Description("The URL to the Seq sink.")]
    public string SeqSinkUrl { get; set; }

    #endregion

    #region Login Server

    [IniSection("Login Server")]

    [DefaultValue("Imlight.Login")]
    [Description("The internal name for the login server. This is not a realm name.")]
    public string? LoginServerName { get; set; }

    [DefaultValue(12000)]
    public ushort LoginServerPort { get; set; }

    [DefaultValue(1000)]
    public ushort LoginPlayerLimit { get; set; }

    [DefaultValue(6)]
    public byte MaxAllowedCharactersPerAccount { get; set; }

    [DefaultValue(360)]
    [Description("The time in seconds that a user can be AFK before being disconnected.")]
    public ushort LoginAfkTimeout { get; set; }

    [DefaultValue(60)]
    [Description("The time in seconds between AFK checks.")]
    public ushort LoginAfkCheckInterval { get; set; }

    #endregion

    #region Game Server

    [IniSection("Game Server")]

    [DefaultValue("Imlight.Game")]
    [Description("The internal name for the game server. This is not a realm name.")]
    public string? GameServerName { get; set; }

    [DefaultValue(12333)]
    public ushort GameServerPort { get; set; }

    [DefaultValue(1000)]
    public ushort GameServerPlayerLimit { get; set; }

    [DefaultValue(3)]
    [Description("The number of game servers that can be created.")]
    public byte MaxGameServersAllowed { get; set; }

    [DefaultValue(28800)]
    [Description("The time in seconds that a session key is valid.")]
    public ushort SessionKeyValidityTime { get; set; }

    #endregion

    #region Character

    [IniSection("Character")]

    [DefaultValue(1)]
    public byte StartingLevel { get; set; }

    [DefaultValue("WizardCity/WC_Hub")]
    public string? StartingZone { get; set; }

    [DefaultValue(1)]
    public byte StartingWorld { get; set; }

    [DefaultValue(1000000)]
    public int BaseGoldPouch { get; set; }

    [DefaultValue(Byte.MaxValue)]
    public byte MaxLevel { get; set; }

    [DefaultValue(75)]
    public int MaxInventoryItems { get; set; }

    [DefaultValue(100)]
    public int MaxJewelsAllowed { get; set;}

    #endregion

    #region Patch Server

    [IniSection("Patch Server")]

    [DefaultValue("Imlight.Patch")]
    [Description("The internal name for the patch server. This is not a realm name.")]
    public string? PatchServerName { get; set; }

    [DefaultValue(12500)]
    [Description("The port used by the patch server.")]
    public ushort PatchServerPort { get; set; }

    [DefaultValue("http://phill030.de")]
    [Description("The URL to the patch server.")]
    public string? PatchServerInternalUrl { get; set; }

    [DefaultValue(10)]
    [Description("The time in seconds that the patch server will wait to reach the endpoint before timing out.")]
    public uint PatchServerInternalTimeout { get; set; }

    [DefaultValue("./cache")]
    public string LocalWadCachePath { get; set; }

    [DefaultValue(360)]
    [Description("The time in seconds that any given download will wait before timing out.")]
    public int PatchServerDownloadTimeout { get; set; }

    #endregion

    #region Database

    [IniSection("Database")]

    [DefaultValue("")]
    public string? PlayerDatabaseUrl { get; set; }

    [DefaultValue("Playerdata")]
    public string? PlayerDatabaseName { get; set; }

    [DefaultValue("./Certificates/dragon.admin.pfx")]
    public string? PlayerDatabaseCertificatePath { get; set; }

    [DefaultValue("")]
    public string? WorldDatabaseUrl { get; set; }

    [DefaultValue("Imlight")]
    public string? WorldDatabaseName { get; set; }

    [DefaultValue("./Certificates/worlddata.client.pfx")]
    public string? WorldDatabaseCertificatePath { get; set; }

    [DefaultValue(16)]
    public byte DatabaseMaxNumberOfRequestsPerSession { get; set; }

    [DefaultValue(90)]
    public byte DatabaseRequestTimeoutInSeconds { get; set; }

    [DefaultValue(5)]
    public byte DatabaseWaitForNonStaleResultsTimeout { get; set; }

    [DefaultValue("./ImlightEmbeddedDatabase")]
    [Description("The directory where the embedded database will be stored.")]
    public string? EmbeddedDatabaseDataDirectory { get; set; }

    [DefaultValue(8080)]
    [Description("The port used by the embedded database.")]
    public ushort EmbeddedDatabasePort { get; set; }

    [DefaultValue(90)]
    [Description("The time in seconds that the embedded database will wait to reach the endpoint before timing out.")]
    public ushort EmbeddedDatabaseTimeoutTime { get; set; }

    [DefaultValue(false)]
    [Description("If true, a full RavenDb database will be used. The full database includes a dotnet runtime.")]
    public bool EmbeddedDatabaseUseFull { get; set; }

    [Description("The path to the full RavenDb. Only used if EmbeddedDatabaseUseFull is true.")]
    public string? EmbeddedDatabaseFullPath { get; set; }

    #endregion

    #region Advanced

    [IniSection("Advanced")]
    [Description("Please only change these settings if you know what you are doing.")]

    [DefaultValue(6)]
    public byte CharacterUploadIntervalInMinutes { get; set; }

    [DefaultValue("MAGIC_HATTER")]
    [Description("The salt used to hash the session key.")]
    public string? SessionKeyHashInput { get; set; }

    [DefaultValue(4096)]
    public ushort PatchServerBufferSize { get; set; }

    [DefaultValue("KingsIsle Patcher")]
    public string? PatchServerUserAgent { get; set; }

    [DefaultValue(4096)]
    [Description("The size of the buffer used by the session actor.")]
    public int SessionActorBufferSize { get; set; }

    [DefaultValue(3)]
    [Description("The thread pool size used by the session actor to send messages.")]
    public byte SessionActorSendPoolSize { get; set; }

    [DefaultValue(3)]
    [Description("The thread pool size used by the session actor to receive messages.")]
    public byte SessionActorReceivePoolSize { get; set; }

    [DefaultValue(true)]
    [Description("If true, the session actor will close on exception.")]
    public bool SessionActorCloseOnException { get; set; }

    [DefaultValue(3)]
    [Description("The number of times the session actor will restart a service before crashing.")]
    public byte SessionActorServiceRetryCount { get; set; }

    [DefaultValue(30)]
    [Description("The time in seconds that the session actor will wait before restarting.")]
    public byte SessionActorServiceRangeRetry { get; set; }

    [DefaultValue(60)]
    [Description("The time in seconds that the server will send a heartbeat to the client.")]
    public byte KeepAliveInterval { get; set; }

    [DefaultValue(4)]
    [Description("The time in seconds the server will wait for a heartbeat response.")]
    public byte KeepAliveRspWaitTime { get; set; }

    [DefaultValue(15)]
    [Description("How many requests are allowed within the token bucket of the session actor.")]
    public int SessionTokenBucketMax { get; set; }

    [DefaultValue(10)]
    [Description("How many new tokens are added to the token bucket per second.")]
    public int SessionTokenBucketPerSecond { get; set; }

    [DefaultValue(5)]
    [Description("How many times the session actor will try to acquire a token before failing.")]
    public byte SessionTokenBucketFailedAcquisitionLimit { get; set; }

    [DefaultValue(10)]
    [Description("The time in seconds that the local wad cache will wait for the patch server to respond.")]
    public int LocalWadCacheWaitForPatchServerTimeout { get; set; }

    #endregion
}
