namespace Imlight.Common.Configuration;

[IniSection("Global Settings")]
public sealed class ServerSettings
{
    [DefaultValue(739602)]
    public uint GameRevision { get; set; }
    
    #region Logging
    
    [IniSection("Logging")]
    
    [DefaultValue("./logs/log.txt")]
    public string LogPath { get; set; }
    
    [DefaultValue(true)]
    public bool LogsIncludeTimestamp { get; set; }
    
    #endregion

    #region Login Server
    
    [IniSection("Login Server")]
    
    [DefaultValue("Imlight.Login")]
    [Description("The internal name for the login server. This is not a realm name.")]
    public string LoginServerName { get; set; }
    
    [DefaultValue(12000)]
    public ushort LoginServerPort { get; set; }
    
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
    public string GameServerName { get; set; }
    
    [DefaultValue(12333)]
    public ushort GameServerPort { get; set; }
    
    [DefaultValue(28800)]
    [Description("The time in seconds that a session key is valid.")]
    public ushort SessionKeyValidityTime { get; set; }
    
    #endregion
    
    #region Patch Server
    
    [IniSection("Patch Server")]
    
    [DefaultValue("Imlight.Patch")]
    [Description("The internal name for the patch server. This is not a realm name.")]
    public string PatchServerName { get; set; }
    
    [DefaultValue(12500)]
    [Description("The port used by the patch server.")]
    public ushort PatchServerPort { get; set; }
    
    [DefaultValue("http://phill030.de")]
    [Description("The URL to the patch server.")]
    public string PatchServerInternalUrl { get; set; }
    
    [DefaultValue(12369)]
    [Description("The internal port used by the patch server.")]
    public ushort PatchServerInternalPort { get; set; }
    
    [DefaultValue(10)]
    [Description("The time in seconds that the patch server will wait to reach the endpoint before timing out.")]
    public uint PatchServerTimeout { get; set; }
    
    #endregion

    #region Advanced

    [IniSection("Advanced")] 
    [Description("Please only change these settings if you know what you are doing.")]

    [DefaultValue(6)]
    public byte CharacterUploadIntervalInMinutes { get; set; }
    
    [DefaultValue("MAGIC_HATTER")]
    [Description("The salt used to hash the session key.")]
    public string SessionKeyHashInput { get; set; }
    
    [DefaultValue(4096)]
    public ushort PatchServerBufferSize { get; set; }
    
    [DefaultValue("KingsIsle Patcher")]
    public string PatchServerUserAgent { get; set; }
    
    #endregion
}