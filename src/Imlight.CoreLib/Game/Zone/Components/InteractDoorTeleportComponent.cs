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
 * INTERACT DOOR TELEPORT COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Makes an "Enter door" object (an InteractableBehavior template such as the Krokotopia School
 * doors) a press-X interactable that sends the player to a fixed zone and location. The client
 * ships no destination for these doors, so it comes from the zone's teleport table.
 * 
 * USAGE EXAMPLE:
 * Add a ZoneTransfer teleport to the placing zone's file whose TriggerName is the door template's
 * object name (GameObjectTemplate.m_objectName); the door is then usable in that zone.
 * 
 * NOTE:
 * The option is offered through the shared service memento (MSG_SENDNPCOPTIONS), which the client
 * handles exactly like live's MSG_SENDINTERACTOPTIONS. A door whose name has no teleport in the
 * current zone stays inert. The transfer is the same MSG_ZONETRANSFER a trigger ResTeleport sends.
 * 
 * TODO:
 * 
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 09/29/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractDoorTeleportComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const string DOOR_TEXT_KEY = "GUI_EnterDoor";

    // Every teleport name the zone data knows, built on first use (the zone data is loaded before any zone).
    private static HashSet<string> s_teleportNames;

    public string ServiceName => "Interact";
    public string NpcIcon => (Entity.Template as GameObjectTemplate)?.m_sIcon;
    public string NpcNameKey => (Entity.Template as GameObjectTemplate)?.m_displayName;
    public string NpcTextKey => DOOR_TEXT_KEY;
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    public static bool ShouldAttachToEntity(CoreTemplate template) {
        if (template is not GameObjectTemplate go || go.m_objectName is null) {
            return false;
        }

        return HasInteractableBehavior(go) && GetTeleportNames().Contains(go.m_objectName.ToString());
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) {
        if (!TryResolveTeleport(out _)) {
            return [];
        }

        return [
            new InteractableOption { m_serviceName = ServiceName }
        ];
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (!TryResolveTeleport(out var teleport)) {
            return;
        }

        // The same request a trigger's ResTeleport sends: the session readies the client and transfers.
        playerActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = teleport.m_destinationZone,
            DestinationLocation = teleport.m_destinationLoc,
            SendToClient = true,
            OwnerCharId = playerCharacter.CharId
        });
    }

    private bool TryResolveTeleport(out ResTeleport teleport) {
        teleport = null;
        var zonePath = Entity.Zone?.ZonePath;
        var name = (Entity.Template as GameObjectTemplate)?.m_objectName?.ToString();
        if (string.IsNullOrEmpty(zonePath) || string.IsNullOrEmpty(name)) {
            return false;
        }

        teleport = ZoneDataCollection.GetZoneData(zonePath)?.Teleports?
            .FirstOrDefault(t => t.TriggerName == name)?.Teleport;

        return !string.IsNullOrEmpty(teleport?.m_destinationZone);
    }

    private static bool HasInteractableBehavior(GameObjectTemplate template)
        => template.m_behaviors?.OfType<InteractableBehaviorTemplate>().Any() == true;

    private static HashSet<string> GetTeleportNames() {
        if (s_teleportNames is not null) {
            return s_teleportNames;
        }

        var names = SpiralDB.GetAllZoneData()
            .Where(zone => zone?.Teleports is not null)
            .SelectMany(zone => zone.Teleports)
            .Select(t => t.TriggerName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.Ordinal);

        // An empty set means the zone data is not loaded yet: do not cache it.
        if (names.Count > 0) {
            s_teleportNames = names;
        }

        return names;
    }

}
