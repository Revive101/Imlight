/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
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
