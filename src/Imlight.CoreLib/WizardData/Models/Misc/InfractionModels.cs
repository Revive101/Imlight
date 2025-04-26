/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Imlight.CoreLib.WizardData.Models.Misc;

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
    public bool WasWaived { get; set; }
    public string WasWaivedBy { get; set; }

}

public class InfractionHistory {

    public ulong AccountId { get; set; }
    public List<Infraction> Infractions { get; set; }
    public bool IsCurrentlyBanned
        => Infractions.Any(x => x.InfractionType == InfractionType.Ban && !x.IsExpired && !x.WasWaived);
    public bool IsCurrentlyMuted
        => Infractions.Any(x => x.InfractionType == InfractionType.Mute && !x.IsExpired && !x.WasWaived);
    public DateTime LastInfractionTime => Infractions.Max(x => x.InfractionTime);
    public DateTime BanEndsAt
        => Infractions.Where(x => x.InfractionType == InfractionType.Ban && !x.WasWaived).Max(x => x.Expiration.Value);
    public DateTime MuteEndsAt
        => Infractions.Where(x => x.InfractionType == InfractionType.Mute && !x.WasWaived).Max(x => x.Expiration.Value);

    public InfractionHistory(ulong accountId, List<Infraction> infractions) {
        AccountId = accountId;
        Infractions = infractions;
    }

    public void AddInfraction(Infraction infraction) {
        Infractions ??= [];
        Infractions.Add(infraction);
    }

}

internal class MachineBanRecord {
    public MachineBanRecord(ulong machineId, DateTime banExpiration) {
        
        MachineId = machineId;
        BanExpiration = banExpiration;
    }

    internal ulong MachineId { get; set; }
    internal DateTime BanExpiration { get; set; }
    
}

internal class IpBanRecord {
    public IpBanRecord(string ip, DateTime banExpiration) {
        
        Ip = ip;
        BanExpiration = banExpiration;
    }

    internal string Ip { get; set; }
    internal DateTime BanExpiration { get; set; }
    
}
