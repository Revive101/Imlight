using System;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Models;

public enum InfractionType {
    SuspiciousBehavior,
    Warn,
    Mute,
    Ban
}

public class Infraction {
    public ulong Id { get; set; }
    public ulong AccountId { get; set; }
    public ulong MachineId { get; set; }
    public InfractionType InfractionType { get; set; }
    public DateTime InfractionTime { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; }
    public DateTime? Expiration { get; set; }
    public bool IsExpired => Expiration.HasValue && Expiration.Value < DateTime.UtcNow;
    public string ResponsibleModerator { get; set; }
}

public class InfractionHistory {
    public ulong AccountId { get; set; }
    public Infraction[] Infractions { get; set; }
    public bool IsCurrentlyBanned => Infractions.Any(x => x.InfractionType == InfractionType.Ban && !x.IsExpired);
    public bool IsCurrentlyMuted => Infractions.Any(x => x.InfractionType == InfractionType.Mute && !x.IsExpired);
}
