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
 *
 * ========================================================================
 * ZONE STATUE SUPERVISOR
 * ========================================================================
 *
 * PURPOSE:
 * Spawns the statue definitions that belong to a zone and forwards zone messages to them.
 *
 * USAGE EXAMPLE:
 * Created by Zone next to the other supervisors; spawns on MSG_ZONELOADRESULTS.
 *
 * NOTE:
 * A definition whose base template is not a player template is skipped with one warning.
 *
 * TODO:
 *
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Statues;
using Imlight.CoreLib.WizardData.Models.World;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;

namespace Imlight.CoreLib.Game.Zone.Supervisors;

/// <summary>
/// Spawns the statues of a zone from SpiralDB and forwards zone messages to them.
/// </summary>
internal sealed class ZoneStatueSupervisor(Core.Zone zone) : ZoneEntitySupervisor(zone) {

    private const string ZONE_TAG_PREFIX = "Statue_";

    private readonly Dictionary<string, (IActorRef Actor, CoreObject Object)> _statues = [];

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS))]
    public override void ReceiveZoneLoadResults(ZONE_102_PROTOCOL.MSG_ZONELOADRESULTS message) {
        foreach (var statue in PlayerStatueCollection.GetInZone(Zone.ZonePath)) {
            SpawnStatue(statue);
        }

        ReportLoadedWhenEntitiesLoad();
    }

    protected override void OnEntityLoadFailed(IActorRef entityActor) {
        foreach (var id in _statues.Where(x => x.Value.Actor.Equals(entityActor)).Select(x => x.Key).ToList()) {
            _statues.Remove(id);
        }
    }

    private void SpawnStatue(PlayerStatue statue) {
        var template = CoreObjectFactory.GetCoreTemplate(statue.BaseTemplateId);
        if (!StatueLook.IsPlayerBase(template)) {
            Logger.Warning("Statue {0} has a base template that cannot be a statue; skipping.",
                Logger.Args(statue.Id));

            return;
        }

        var info = new CoreObjectInfo {
            m_templateID = statue.BaseTemplateId,
            m_location = statue.Location,
            m_orientation = statue.Orientation,
            m_fScale = 1,
            m_zoneTag = ZONE_TAG_PREFIX + statue.Id,
        };
        var coreObject = CoreObjectFactory.FinalizeCoreObject(info, template);
        if (coreObject is null) {
            Logger.Warning("Could not finalize statue {0}; skipping.", Logger.Args(statue.Id));

            return;
        }

        coreObject.m_globalID = StatueIdentity.CreateGid(statue.Id);

        var actor = Context.ActorOf(
            Props.Create(() => new StatueEntity(coreObject, template, info, ZoneRef, Zone, statue)),
            CreateEntityActorName(coreObject));
        BeginEntityLoad(actor, statue.Id);
        _statues[statue.Id] = (actor, coreObject);
    }

}
