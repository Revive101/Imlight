namespace Imlight.Common.Configuration;

[IniSection("Global Settings")]
public sealed class ServerSettings
{
    [IniSection("Logging")]
    
    [DefaultValue("./logs/log.txt")]
    public string LogPath { get; set; }
    
    [DefaultValue(true)]
    public bool LogsIncludeTimestamp { get; set; }

    [IniSection("Login Server")]
    
    [DefaultValue("Imlight.Login")]
    [Description("The internal name for the login server. This is not a realm name.")]
    public string LoginServerName { get; set; }
}