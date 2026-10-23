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
 * INTERACT SEAMSTRESS COMPONENT
 * ========================================================================
 *
 * PURPOSE:
 * Makes the NPCs that SpiralDB marks as seamstresses offer stitching and unstitching. Picking an option
 * opens the client's stitch or unstitch window.
 *
 * USAGE EXAMPLE:
 * Same IServiceComponent + IComponentFactory shape as InteractDyeShopComponent, found by reflection.
 * The requests themselves are handled by SeamstressService.
 *
 * NOTE:
 * Attaches only to NPC templates that have an NpcSeamstress record in SpiralDB, and only while both Crowns
 * prices are configured. Both options are always offered together.
 *
 * TODO:
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Seamstress;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractSeamstressComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    private const uint STITCH_OPTION_INDEX = 0;

    public string ServiceName     => "Seamstress";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => "WizardShops_00000074";
    public WizBangs WizBang       => WizBangs.Shopping;
    public string StateName       => "Shop";
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_SeamstressCrowns";

    public static bool ShouldAttachToEntity(CoreTemplate template)
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && NpcSeamstressCollection.TryGetSeamstress(goTemplate.m_templateID, out _)
        && SeamstressConfig.TryGetPrices(out _, out _);

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new SeamstressOption {
                m_displayKey = DisplayKey,
                m_iconKey = "SeamstressOption",
                m_serviceName = ServiceName,
            },
            new UnstitchOption {
                m_displayKey = "GUI_UnstitchCrowns",
                m_iconKey = "UnstitchOption",
                m_serviceName = "Unstitch",
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        if (!SeamstressConfig.TryGetPrices(out var stitchCrowns, out var unstitchCrowns)) {
            return;
        }

        if (serviceOptionIndex == STITCH_OPTION_INDEX) {
            playerActor.Tell(new WIZARD_12_PROTOCOL.MSG_SEAMSTRESSOPEN {
                GlobalID = Entity.ActiveGameObject.m_globalID,
                ShopTitle = NpcTextKey,
                Credits = playerCharacter.Account.Crowns,
                MergeCost = stitchCrowns,
                WebFailure = 0
            });
        } else {
            playerActor.Tell(new WIZARD_12_PROTOCOL.MSG_UNSTITCHOPEN {
                GlobalID = Entity.ActiveGameObject.m_globalID,
                ShopTitle = NpcTextKey,
                Credits = playerCharacter.Account.Crowns,
                MergeCost = unstitchCrowns,
                WebFailure = 0
            });
        }

        SendPlayerIntoWizbang(playerObject.m_globalID);
        SendPlayerIntoState(playerObject.m_globalID);
    }

    /// <summary>
    /// Whether a player stands close enough to the seamstress to use her. This is the radius at which the
    /// client is offered her options, measured on the ground plane.
    /// </summary>
    /// <param name="playerObject">The player's game object.</param>
    /// <returns>True when the player is within the interaction radius.</returns>
    internal bool IsInServiceRange(CoreObject playerObject)
        => IsInHorizontalRadius(playerObject, ((IServiceComponent) this).DEFAULT_INTERACTION_RADIUS);

    private void SendPlayerIntoWizbang(ulong playerObjID) {
        var wizBangMsg = new GAME_5_PROTOCOL.MSG_WIZBANG {
            WizBangID = (uint) WizBang,
            GameObjectID = playerObjID
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = wizBangMsg,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }

    private void SendPlayerIntoState(ulong playerObjID) {
        var changeStateMsg = new GAME_5_PROTOCOL.MSG_ENTERSTATE {
            State = StringHash.Compute(StateName),
            GameObjectID = playerObjID
        };
        var broadcastMsg = new ZONE_102_PROTOCOL.MSG_ZONEBROADCAST {
            Message = changeStateMsg,
            Selfless = false,
        };

        Entity.ZoneRef.Tell(broadcastMsg);
    }

}
