using System;

namespace Imlight.CoreLib.WizardData.Models;

public class ChatLog {
    public DateTime TimeStamp { get; set; }
    public string ZoneName { get; set; }
    public ulong CharacterId { get; set; }
    public ulong AccountId { get; set; }
    public string Message { get; set; }
}
