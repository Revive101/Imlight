namespace Imlight.Common.Configuration;

[IniSection("Global Settings")]
public sealed class ServerSettings
{
    // Anything written here without an IniSection is considered global.
    
    [IniSection("Logging")]
    [DefaultValue("./logs/log.txt")]
    public string LogPath { get; set; }

    [IniSection("Login Server")]
    [DefaultValue("Imlight.Login")]
    public string LoginServerName { get; set; }
}