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
 * INTERACT TREASURE VENDOR COMPONENT
 * ========================================================================
 * 
 * PURPOSE:
 * Manages treasure card vendor interaction mechanics for NPCs, handling 
 * treasure card shop inventory and player interactions.
 * 
 * Created by: Reasonix
 * Version: KALI 1.0
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.Cryptography;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractTreasureVendorComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName     => "TreasureShopService";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => null;
    public WizBangs WizBang       => WizBangs.Shopping;
    public string StateName       => "Shop"; // Forbids the player from moving.
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_TreasureShop";

    private List<TreasureCardEntry> _inventory;

    public static bool ShouldAttachToEntity(CoreTemplate template)
        // Attach if the template is an NPC and has a treasure card inventory in SpiralDB.
        => template is GameObjectTemplate goTemplate
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate)
        && NpcTreasureCardInventoryCollection.TryGetInventory(goTemplate.m_templateID, out _);

    public override void OnStart() {
        if (!NpcTreasureCardInventoryCollection.TryGetInventory(Entity.ActiveGameObject.m_templateID, out var inventory)) {
            Logger.Error("Failed to get treasure card inventory for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_templateID));

            return;
        }

        _inventory = inventory.TreasureCards;
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard _)
        => [
            new TreasureShopOption {
                m_displayKey = DisplayKey,
                m_iconKey = NpcIcon,
                m_serviceName = ServiceName,
            }
        ];

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) {
        SendTreasureShopOfferings(playerActor);
        SendPlayerIntoWizbang(playerObject.m_globalID);
        SendPlayerIntoState(playerObject.m_globalID);
    }

    public bool HasSpell(string spellName)
        => _inventory.Any(x => x.SpellName == spellName);

    /// <summary>
    /// Checks if the vendor carries a spell with the given hash.
    /// The hash is computed from the spell name via StringHash.Compute.
    /// </summary>
    public bool HasSpell(uint spellHash)
        => _inventory.Any(x => StringHash.Compute(x.SpellName) == spellHash);

    /// <summary>
    /// Gets the spell name for a given spell hash, or null if not found.
    /// </summary>
    public string GetSpellName(uint spellHash)
        => _inventory.Find(x => StringHash.Compute(x.SpellName) == spellHash)?.SpellName;

    public int GetSpellPrice(string spellName) {
        var entry = _inventory.Find(x => x.SpellName == spellName);
        return entry?.Price ?? 0;
    }

    /// <summary>
    /// Gets the spell price for a given spell hash.
    /// </summary>
    public int GetSpellPrice(uint spellHash) {
        var spellName = GetSpellName(spellHash);
        return spellName != null ? GetSpellPrice(spellName) : 0;
    }

    private void SendTreasureShopOfferings(IActorRef playerActor) {
        var shopOffering = new TreasureShopOffering() {
            m_treasureShopTitle = "Treasure Cards",
            m_treasureSpellNames = _inventory.Select(x => x.SpellName).ToList(),
            m_crownShop = false,
            m_pvpCurrencyShop = false,
            m_pvpTourneyCurrencyShop = false,
        };

        // Serialize the offerings and send them to the player.
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(shopOffering, (PropertyFlags) 31, out var serializedShopList)) {
            Logger.Error("Failed to serialize treasure shop offering for NPC {0}",
                Logger.Args(Entity.ActiveGameObject.m_templateID));

            return;
        }

        var shopListMsg = new WIZARD_12_PROTOCOL.MSG_TREASURESHOPLIST() {
            GlobalID = Entity.ActiveGameObject.m_globalID.Full,
            Data = serializedShopList
        };
        playerActor.Tell(shopListMsg);
    }

    private void SendPlayerIntoWizbang(ulong playerObjID) {
        // Create the wiz bang message, and wrap it in a broadcast message.
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
        // Create the change state message, and wrap it in a broadcast message.
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
