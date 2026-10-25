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
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Game.Cantrips;
using Imlight.CoreLib.Game.World;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.PluginApi;
using Imlight.PluginApi.Events;

namespace Imlight.CoreLib.Plugins;

/// <summary>
/// Exposes a session's wizard to plugins as an <see cref="IPlayer"/>.
/// </summary>
/// <remarks>
/// Each setter changes the wizard the same way the matching admin command does, then sends the client the same update.
/// </remarks>
internal sealed class PluginPlayer(Wizard wizard, IActorRef sessionActor, IZone zone) : IPlayer {

    public ulong CharacterId => wizard.CharId;

    public string Name => wizard.PlayerNameBehavior.GetWizardName();

    public IZone Zone => zone;

    public System.Numerics.Vector3 Location
        => new(wizard.Location.X, wizard.Location.Y, wizard.Location.Z);

    public float Orientation => wizard.Orientation.Z;

    public int Level => wizard.MagicSchoolBehavior.Level;

    public int Experience => wizard.MagicSchoolBehavior.ExperiencePoints;

    public int Gold => wizard.GameStats.m_currentGold;

    public int MaxGold => wizard.GameStats.m_baseGoldPouch;

    public int Health => wizard.GameStats.m_currentHitpoints;

    public int MaxHealth => wizard.GameStats.m_baseHitpoints;

    public int Mana => wizard.GameStats.m_currentMana;

    public int MaxMana => wizard.GameStats.m_baseMana;

    public int Energy => wizard.PetOwnerBehavior.Energy;

    public int MaxEnergy => wizard.GameStats.m_energyMax;

    public int TrainingPoints => wizard.MagicSchoolBehavior.TrainingPoints;

    public int CantripLevel => wizard.GameStats.m_cantripLevel;

    public float PotionCharge => wizard.GameStats.m_potionCharge;

    public float PotionMax => wizard.GameStats.m_potionMax;

    public IReadOnlyList<PlayerItem> Inventory => Snapshot(wizard.InventoryBehavior.Items);

    public IReadOnlyList<PlayerItem> Equipment => Snapshot(wizard.EquipmentBehavior.EquippedItems);

    public bool HasItem(ulong templateId)
        => Inventory.Any(item => item.TemplateId == templateId) || Equipment.Any(item => item.TemplateId == templateId);

    internal IActorRef SessionActor => sessionActor;

    public void SendMessage(string message)
        => sessionActor.Tell(new EXTENDEDBASE_2_PROTOCOL.MSG_SERVERMESSAGE { Message = message });

    public void SetLevel(int level) {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, MagicLevelsConfig.MaxLevel);

        // WizardService applies the level and sends the client every stat it resets.
        sessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_LEVELUP { NewLevel = (byte) level }, null);
    }

    public void AddExperience(int amount) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        // WizardService adds the XP and handles any level up it causes.
        sessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_GAINXP { XP = amount }, null);
    }

    public void AddGold(int amount) {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        wizard.AddGold(amount);
        SendGold();
    }

    public bool RemoveGold(int amount) {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > Gold) {
            return false;
        }

        wizard.RemoveGold(amount);
        SendGold();

        return true;
    }

    public void SetMaxGold(int maxGold) {
        ArgumentOutOfRangeException.ThrowIfNegative(maxGold);

        wizard.SetMaxGold(maxGold);
        SendGold();
    }

    public void SetHealth(int health) {
        wizard.UpdateHealth(Math.Clamp(health, 0, MaxHealth));

        // The client already applies its max health effect, so it gets the level's base max to avoid doubling it.
        SendHealth(BaseStats().m_hitpoints);
    }

    public void SetMaxHealth(int maxHealth) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);

        wizard.UpdateMaxHealth(maxHealth);
        SendHealth(maxHealth);
    }

    public void SetMana(int mana) {
        wizard.UpdateMana(Math.Clamp(mana, 0, MaxMana));

        // The client already applies its max mana effect, so it gets the level's base max to avoid doubling it.
        SendMana(BaseStats().m_mana);
    }

    public void SetMaxMana(int maxMana) {
        ArgumentOutOfRangeException.ThrowIfNegative(maxMana);

        wizard.UpdateMaxMana(maxMana);
        SendMana(maxMana);
    }

    public void SetEnergy(int energy) {
        wizard.UpdateEnergy(Math.Clamp(energy, 0, MaxEnergy));

        // The client already applies its max energy effect, so it gets the level's base max to avoid doubling it.
        sessionActor.Tell(new PET_9_PROTOCOL.MSG_PETENERGYTICK {
            GlobalID = wizard.GameObjectID,
            Energy = Energy,
            MaxEnergy = BaseStats().m_petEnergy,
            TickTime = (int) wizard.PetOwnerBehavior.LastEnergyTickEpoch
        }, null);
    }

    public void SetTrainingPoints(int trainingPoints) {
        ArgumentOutOfRangeException.ThrowIfNegative(trainingPoints);

        wizard.UpdateTrainingPoints(trainingPoints);
        sessionActor.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING { TrainingPoints = trainingPoints }, null);
    }

    public void SetCantripLevel(int level) {
        ArgumentOutOfRangeException.ThrowIfNegative(level);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, CantripFactory.GetMaxCantripLevel());

        wizard.UpdateCantripLevel((byte) level);
        sessionActor.Tell(new CANTRIPSMESSAGES_57_PROTOCOL.MSG_UPDATECANTRIPXP { XP = 0, Level = (byte) level }, null);
    }

    public void SetPotions(float charge, float max) {
        ArgumentOutOfRangeException.ThrowIfNegative(max);

        wizard.UpdatePotions(Math.Clamp(charge, 0, max), max);
        sessionActor.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS {
            PotionCharge = PotionCharge,
            PotionMax = PotionMax
        }, null);
    }

    public void GiveItem(ulong templateId) {
        var error = ItemHelper.CheckGiveable(templateId);
        if (error is not null) {
            throw new ArgumentException(error, nameof(templateId));
        }

        sessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_GIVEITEM { TemplateId = templateId }, null);
    }

    public void RemoveItem(ulong itemId)
        => sessionActor.Tell(new CHARACTER_103_PROTOCOL.MSG_REMOVEITEM { ItemId = itemId }, null);

    public void SetLocation(System.Numerics.Vector3 location)
        => SetLocation(location, Orientation);

    public void SetLocation(System.Numerics.Vector3 location, float orientation)
        => Transfer(wizard.Zone, location, orientation);

    public void SetOrientation(float orientation)
        => SetLocation(Location, orientation);

    public void TransferZone(string zone)
        => Transfer(zone, "Start");

    public void TransferZone(string zone, System.Numerics.Vector3 location, float orientation)
        => Transfer(zone, location, orientation);

    private void Transfer(string zone, System.Numerics.Vector3 location, float orientation)
        => Transfer(zone, Util.GetCompactStringFromVector(new Imcodec.Math.Vector4(location.X, location.Y, location.Z, orientation)));

    private void Transfer(string zone, string location) {
        if (!AccessPassManager.DoesZoneExist(zone)) {
            throw new ArgumentException($"Zone {zone} does not exist.", nameof(zone));
        }

        // ZoneService teleports in place when the zone is the wizard's own, and fires the plugin event either way.
        sessionActor.Tell(new ZONE_102_PROTOCOL.MSG_ZONETRANSFER {
            DestinationZone = zone,
            DestinationLocation = location,
            SendToClient = true,
            OwnerCharId = wizard.CharId,
            Cause = ZoneChangeCause.Plugin,
        }, null);
    }

    private void SendGold()
        => sessionActor.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD { Gold = Gold, MaxGold = MaxGold }, null);

    private void SendHealth(int clientMaxHealth)
        => sessionActor.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
            CharacterID = wizard.GameObjectID,
            NewHealth = Health,
            NewHealthMax = clientMaxHealth,
            DisplayDiff = 1,
        }, null);

    private void SendMana(int clientMaxMana)
        => sessionActor.Tell(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
            Mana = Mana,
            MaxMana = clientMaxMana,
            DisplayDiff = 1,
        }, null);

    // The session may change the list while a plugin reads it, so this copies it before going through it.
    private static PlayerItem[] Snapshot(List<WizClientObjectItem> items)
        => (items?.ToArray() ?? [])
            .Where(item => item is not null)
            .Select(item => new PlayerItem((ulong) item.m_globalID, (ulong) item.m_templateID, item.m_debugName.ToString()))
            .ToArray();

    private MagicLevelInfo BaseStats()
        => MagicLevelsConfig.GetPlayerLevelInfo(wizard.MagicSchoolBehavior.MagicSchool, wizard.MagicSchoolBehavior.Level);

    /// <summary>
    /// The plugin view of a session, taken from its zone when it is in one.
    /// </summary>
    public static IPlayer For(Wizard wizard, IActorRef sessionActor, IActorRef zoneActor)
        => PluginZone.Of(zoneActor)?.PlayerFor(wizard, sessionActor)
           ?? new PluginPlayer(wizard, sessionActor, null);

}
