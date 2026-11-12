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
 * Makes an "Enter door" or teleporter object (an InteractableBehavior template such as the Krokotopia
 * School doors, the Wizard City street teleporters or the Marleybone teleport stones) a press-X
 * interactable that sends the player to a fixed zone and location. The client ships no destination
 * for these objects, so it comes from the zone's teleport table.
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
 * In the DISCOVERY_ZONES (Marleybone, MooShu, DragonSpire, Celestia and two Zafaria zones) each stone
 * or pad of a link inside the zone must be discovered, by walking within DISCOVERY_RADIUS of it or by
 * using it, and the link teleports only once both of its endpoints are found (TeleporterEndpoints).
 * Each endpoint shows On to a player who has found it and Off before, as an object state sent to that
 * player alone. The client drops a state that arrives before it has built the object, and rebuilds a
 * far object from its placed state when the player comes back in range, so the look is sent again
 * several times after a zone entry and after each return into range.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 11/12/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractDoorTeleportComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory, IWithTimers {

    private const string DOOR_TEXT_KEY = "GUI_EnterDoor";

    // Where teleporters must be discovered before they work: the zones whose client data marks the far stone with a
    // quest-marker persona and has a trigger light the pair (Zafaria's hub stones and town links have neither).
    private static readonly string[] DISCOVERY_ZONES = [
        "Marleybone/", "MooShu/", "DragonSpire/", "Celestia/",
        "Zafaria/ZF_Z03_Savannah", "Zafaria/ZF_Z09_Drum_Jungle",
    ];
    private const float DISCOVERY_RADIUS = 200f;
    private const string FOUND_STATE = "On";
    private const string NOT_FOUND_STATE = "Off";

    // Each delay counts from the send before it: every quarter second for the first three seconds after a zone entry
    // or a return into range (the client builds the object in that time), then at about 5 and 10 seconds.
    private static readonly TimeSpan[] LOOK_REFRESH_DELAYS = [
        .. Enumerable.Repeat(TimeSpan.FromSeconds(0.25), 12),
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)
    ];
    // A player beyond this share of the zone's far clip no longer has the object built on their client.
    private const float DRAW_RANGE_FRACTION = 0.9f;

    // Every teleport name the zone data knows, built on first use (the zone data is loaded before any zone).
    private static HashSet<string> s_teleportNames;

    // True when this object is a discoverable endpoint of a link in this zone.
    private bool _discoverable;
    // The players within draw range of this endpoint, and the look each was last sent.
    private readonly HashSet<IActorRef> _inRange = [];
    private readonly Dictionary<IActorRef, bool> _lastLook = [];

    public ITimerScheduler Timers { get; set; }

    public string ServiceName => "Interact";
    public string NpcIcon => (Entity.Template as GameObjectTemplate)?.m_sIcon;
    public string NpcNameKey => (Entity.Template as GameObjectTemplate)?.m_displayName;
    // The object's own prompt ("Press X to Teleport") when the template names one.
    public string NpcTextKey => Interactable?.m_interactTextOverride is { Length: > 0 } text ? text : DOOR_TEXT_KEY;
    public WizBangs WizBang => WizBangs.None;
    public string StateName => null;
    public string InteractWizBang => null;
    public string DisplayKey => null;

    private InteractableBehaviorTemplate Interactable
        => (Entity.Template as GameObjectTemplate)?.m_behaviors?.OfType<InteractableBehaviorTemplate>().FirstOrDefault();

    // The template name the zone's teleport table keys the link by, unique among a zone's linked endpoints. Placed
    // tags are not: the client data copies them (two Storm Haven stones are both "CL_TeleportStoneAMain instance").
    private string EndpointName => (Entity.Template as GameObjectTemplate)?.m_objectName.ToString();

    public static bool ShouldAttachToEntity(CoreTemplate template) {
        if (template is not GameObjectTemplate go || go.m_objectName.IsEmpty) {
            return false;
        }

        return HasInteractableBehavior(go) && GetTeleportNames().Contains(go.m_objectName.ToString());
    }

    public override void OnStart() {
        // Only a link inside the zone has both endpoints here; a door to another zone is never gated.
        var zonePath = Entity.Zone?.ZonePath ?? string.Empty;
        if (Entity.Info is not { } info
            || !DISCOVERY_ZONES.Any(prefix => zonePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || !TryResolveTeleport(out var teleport)
            || !string.Equals(teleport.m_destinationZone, zonePath, StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var destination = Util.GetVectorFromCompactString(teleport.m_destinationLoc);
        Zone.TeleporterEndpoints.Register(EndpointName,
            new Vector2(info.m_location.X, info.m_location.Y),
            new Vector2(destination.X, destination.Y));
        _discoverable = true;
    }

    public override void OnPlayerJoin(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!_discoverable || playerActor is null) {
            return;
        }

        // The first move counts as coming into range, which starts the refreshes again.
        _inRange.Remove(playerActor);
        ShowLook(playerActor, IsFound(playerWizard));
    }

    public override void OnPlayerLeave(IActorRef playerActor, ulong id) {
        _inRange.Remove(playerActor);
        _lastLook.Remove(playerActor);
        CancelRefreshes(playerActor);
    }

    public override void OnPlayerMove(CoreObject playerObj, IActorRef playerActor, Wizard playerWizard) {
        if (!_discoverable || playerObj is null || playerWizard is null || Entity.Info is not { } info) {
            return;
        }

        var dx = playerObj.m_location.X - info.m_location.X;
        var dy = playerObj.m_location.Y - info.m_location.Y;
        var dz = playerObj.m_location.Z - info.m_location.Z;
        var planarSquared = dx * dx + dy * dy;

        var drawRange = (Entity.Zone?.ZoneData?.m_farClip ?? 0f) * DRAW_RANGE_FRACTION;
        var inRange = drawRange <= 0 || planarSquared <= drawRange * drawRange;
        if (!inRange) {
            _inRange.Remove(playerActor);
        }
        else if (_inRange.Add(playerActor)) {
            ShowLook(playerActor, IsFound(playerWizard));
        }

        if (planarSquared + dz * dz <= DISCOVERY_RADIUS * DISCOVERY_RADIUS) {
            Discover(playerActor, playerObj, playerWizard);
        }
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter) {
        if (!TryResolveTeleport(out _)) {
            return [];
        }

        // The options are asked for as the player steps up to this endpoint, which finds it; the link still needs its other end.
        if (_discoverable && !IsPartnerFound(playerCharacter)) {
            return [];
        }

        return [
            new InteractableOption { m_serviceName = ServiceName }
        ];
    }

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (_discoverable) {
            Discover(playerActor, playerObject, playerCharacter);
            if (!IsPartnerFound(playerCharacter)) {
                return;
            }
        }

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

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_TELEPORTERLOOKREFRESH))]
    private void ReceiveLookRefresh(ZONE_102_PROTOCOL.MSG_TELEPORTERLOOKREFRESH message) {
        if (message.PlayerActor is null || !_lastLook.TryGetValue(message.PlayerActor, out var found)) {
            return;
        }

        SendLook(message.PlayerActor, found);
        ScheduleRefresh(message.PlayerActor, message.Step + 1);
    }

    /// <summary>
    /// Records that the player found this endpoint (saved as a registry entry) and lights it for them.
    /// </summary>
    private void Discover(IActorRef playerActor, CoreObject playerObject, Wizard wizard) {
        if (wizard is null || playerActor is null) {
            return;
        }

        var zonePath = Entity.Zone?.ZonePath;
        var alreadySaved = Zone.TeleporterEndpoints.IsFound(wizard, zonePath, EndpointName);
        if (!Zone.TeleporterEndpoints.MarkFound(wizard.CharId, EndpointName) || alreadySaved) {
            return;
        }

        Entity.ExecuteResults(new ResultList {
            m_results = [
                new ResModifyEntry { m_entryName = TeleporterEndpoints.EntryName(zonePath, EndpointName), m_value = 1 }
            ]
        }, playerActor, playerObject, EndpointName);
        ShowLook(playerActor, found: true);

        Logger.Debug("Character {CharId} found teleporter {Object} in {Zone}.",
            Logger.Args(wizard.CharId, EndpointName, zonePath));
    }

    private bool IsFound(Wizard wizard)
        => Zone.TeleporterEndpoints.IsFound(wizard, Entity.Zone?.ZonePath, EndpointName);

    private bool IsPartnerFound(Wizard wizard) {
        var partner = Zone.TeleporterEndpoints.FindPartner(EndpointName);

        return partner is not null && Zone.TeleporterEndpoints.IsFound(wizard, Entity.Zone?.ZonePath, partner);
    }

    // Sends the look now and keeps sending it for a few seconds, until the client has surely built the object.
    private void ShowLook(IActorRef playerActor, bool found) {
        CancelRefreshes(playerActor);
        SendLook(playerActor, found);
        ScheduleRefresh(playerActor, 0);
    }

    // The client ignores dynamic mods on these stones, so the state goes straight to the object, for this player only.
    private void SendLook(IActorRef playerActor, bool found) {
        _lastLook[playerActor] = found;
        Entity.ChangeStateExclusiveSender(found ? FOUND_STATE : NOT_FOUND_STATE, playerActor);
    }

    private void ScheduleRefresh(IActorRef playerActor, int step) {
        if (Timers is null || step >= LOOK_REFRESH_DELAYS.Length) {
            return;
        }

        Timers.StartSingleTimer(RefreshKey(playerActor),
            new ZONE_102_PROTOCOL.MSG_TELEPORTERLOOKREFRESH { PlayerActor = playerActor, Step = step },
            LOOK_REFRESH_DELAYS[step]);
    }

    private void CancelRefreshes(IActorRef playerActor)
        => Timers?.Cancel(RefreshKey(playerActor));

    private static string RefreshKey(IActorRef playerActor)
        => $"teleporter-look-{playerActor.Path.Name}";

    private bool TryResolveTeleport(out ResTeleport teleport) {
        teleport = null;
        var zonePath = Entity.Zone?.ZonePath;
        var name = (Entity.Template as GameObjectTemplate)?.m_objectName.ToString();
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
