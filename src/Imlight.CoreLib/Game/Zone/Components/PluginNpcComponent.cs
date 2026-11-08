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
 * Created by: phazejeff
 * Version: KALI 1.0
 * Last Updated: 11/08/2026
 */

using System;
using System.Collections.Generic;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Plugins;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.PluginApi.Npcs;

namespace Imlight.CoreLib.Game.Zone.Components;

/// <summary>
/// Runs a plugin NPC: offers its interaction, and applies the state changes and removal its plugin sends.
/// Only <see cref="PluginNpcEntity"/> attaches it.
/// </summary>
internal sealed class PluginNpcComponent(ZoneEntity entity)
    : ZoneEntityComponent(entity), IServiceComponent, IClientBehaviorProvider<NPCBehavior> {

    public string ServiceName     => "Interact";
    public string NpcIcon         => (Entity.Template as GameObjectTemplate)?.m_sIcon;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => Npc.WizBang.ToWizBang();
    public string StateName       => null;
    public string InteractWizBang => null;
    public string DisplayKey      => null;

    public bool NoTransfer { get; set; } = false;

    private PluginNpc Npc => ((PluginNpcEntity) Entity).Npc;

    // The zone tag is unique to this NPC, so the zone's script state carries its state to players who arrive later.
    private string ZoneTag => Entity.Info.m_zoneTag;

    public override void OnAwake() {
        // RenderComponent sends the zone's state for the object when it starts, so a state set before the
        // entity existed has to be in place first.
        if (Npc.State is { } state) {
            _ = Zone.ScriptState.SetObjectState(ZoneTag, state);
        }
    }

    // A template's NPC behavior can mark it a monster, which the client would show it as.
    public NPCBehavior GetClientBehaviorInstance() => new() {
        m_isMonster = false,
        m_wsNameOverride = Npc.NameOverride,
    };

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard playerCharacter)
        => Npc.Interaction is null ? [] : [new InteractableOption { m_serviceName = ServiceName }];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        var handler = Npc.Interaction;
        if (handler is null || Npc.IsRemoved) {
            return;
        }

        var player = PluginPlayer.For(playerCharacter, playerActor, ZoneActor);
        try {
            handler(new NpcInteraction(player, Npc));
        }
        catch (Exception ex) {
            // A broken plugin must never take down the NPC's entity.
            Logger.Error("Plugin {Name} threw handling an interaction with NPC {Npc}: {Exception}",
                Logger.Args(Npc.Owner.Name, Npc.Name, ex));
        }
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SETPLUGINNPCSTATE))]
    private void ReceiveSetState(ZONE_102_PROTOCOL.MSG_SETPLUGINNPCSTATE message) {
        _ = Zone.ScriptState.SetObjectState(ZoneTag, message.State);
        Entity.ChangeState(message.State);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_REMOVEPLUGINNPC))]
    private void ReceiveRemove() {
        PlayerBroadcast(new GAME_5_PROTOCOL.MSG_REMOVEOBJECT {
            GameObjectID = Entity.ActiveGameObject.m_globalID
        });
        ZoneActor.Tell(new ZONE_102_PROTOCOL.MSG_RELEASEMOBILEID {
            MobileId = Entity.MobileID
        });

        // The entity hosts this component; stopping it stops every component with it.
        Entity.SelfRef.Tell(PoisonPill.Instance);
    }

}
