using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Models;

public enum InfractionType {
    SuspiciousBehavior,
    Warn,
    Mute,
    Ban
}

public class Infraction {
    public ulong InfractionId { get; set; }
    public ulong AccountId { get; set; }
    public ulong MachineId { get; set; }
    public InfractionType InfractionType { get; set; }
    public DateTime InfractionTime { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; }
    public DateTime? Expiration { get; set; }
    public bool IsExpired => Expiration.HasValue && Expiration.Value < DateTime.UtcNow;
    public string ResponsibleModerator { get; set; } = "Imlight";
}

public class InfractionHistory {
    public ulong AccountId { get; set; }
    public List<Infraction> Infractions { get; set; }
    public bool IsCurrentlyBanned => Infractions.Any(x => x.InfractionType == InfractionType.Ban && !x.IsExpired);
    public bool IsCurrentlyMuted => Infractions.Any(x => x.InfractionType == InfractionType.Mute && !x.IsExpired);
    public DateTime LastInfractionTime => Infractions.Max(x => x.InfractionTime);
    public DateTime BanEndsAt => Infractions.Where(x => x.InfractionType == InfractionType.Ban).Max(x => x.Expiration.Value);
    public DateTime MuteEndsAt => Infractions.Where(x => x.InfractionType == InfractionType.Mute).Max(x => x.Expiration.Value);

    public InfractionHistory(ulong accountId, List<Infraction> infractions) {
        AccountId = accountId;
        Infractions = infractions;
    }

    public void AddInfraction(Infraction infraction) {
        if (Infractions is null) {
            Infractions = new List<Infraction>();
        }

        Infractions.Add(infraction);
    }
}
