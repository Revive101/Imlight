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
 * PET SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages pet energy mechanics and periodic energy regeneration 
 * for player characters.
 * 
 * USAGE EXAMPLE:
 * Internal service handling pet energy tick and synchronization 
 * within the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 06/14/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.IO;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class PetService(SessionActor sessionActor) : MessageService(sessionActor), IWithTimers {

    private static readonly int s_petEnergyTickIntervalInSeconds 
        = ConfigurationManager.Settings["Character.PetEnergyTickInSeconds"].AsInt();
    private const int PET_ENERGY_TICK_DELAY = 2;
    private const int DEFAULT_PET_OVERALL_RATING = 30;

    public ITimerScheduler Timers { get; set; }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PetService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;

        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        var tickTime = petOwnerBehavior.LastEnergyTickEpoch;
        if (tickTime <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
            tickTime = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + s_petEnergyTickIntervalInSeconds);
        }

        var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = wizard.CharId,
            Energy = petOwnerBehavior.Energy,
            MaxEnergy = normMaxEnergy,
            TickTime = (int) tickTime
        };

        SendToSocket(tickMsg);

        var tickTimeSeconds = tickTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var period = TimeSpan.FromSeconds(tickTimeSeconds + PET_ENERGY_TICK_DELAY);
        Timers.StartPeriodicTimer("petEnergyTick", new CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK(), period, period);

        InitializeEggs(wizard);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK))]
    private void ReceiveDoEnergyTick(CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK message) {
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;

        var magicSchool = wizard.MagicSchoolBehavior.MagicSchool;
        var level = wizard.MagicSchoolBehavior.Level;
        var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(magicSchool, level);
        var normMaxEnergy = baseStats.m_petEnergy;

        var tickTime = (int) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + s_petEnergyTickIntervalInSeconds);

        if (petOwnerBehavior.Energy < normMaxEnergy) {
            wizard.UpdateEnergy(petOwnerBehavior.Energy + 1);
            var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
                GlobalID = wizard.CharId,
                Energy = petOwnerBehavior.Energy,
                MaxEnergy = normMaxEnergy,
                TickTime = tickTime
            };

            SendToSocket(tickMsg);
        }
    }

    private void InitializeEggs(Wizard wizard) {
        var morphingSlots = wizard.PetOwnerBehavior.MorphingSlots;
        if (morphingSlots == null || morphingSlots.Count == 0) {
            return;
        }

        // Send creation messages for each egg so the client displays them.
        foreach (var egg in morphingSlots) {
            SendEggCreatedMessages(egg);
        }

        // Hatch any eggs whose timer expired while offline.
        var readyEggs = wizard.PetOwnerBehavior.GetReadyEggs();
        foreach (var egg in readyEggs) {
            HatchEgg(wizard, egg);
        }

        // Start timers for eggs still incubating.
        var pendingEggs = wizard.PetOwnerBehavior.GetPendingEggs();
        foreach (var egg in pendingEggs) {
            StartEggTimer(egg);
        }
    }

    private void SendEggCreatedMessages(CraftingSlot egg) {
        SendToSocket(new PET_9_PROTOCOL.MSG_PETEGGMORPHED {
            PetTemplateGID = egg.m_globalID,
            PetName = 0,
            HatchTime = (uint) egg.m_timeFinished
        });

        SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHINGSLOT {
            GlobalID = egg.m_globalID,
            Removed = 0,
            ExpireTimeCount = (uint) egg.m_timeFinished
        });
    }

    private void StartEggTimer(CraftingSlot egg) {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remainingSeconds = egg.m_timeFinished - (int) now;
        if (remainingSeconds <= 0) {
            // Already ready — hatch immediately.
            var wizard = GetActiveWizard();
            HatchEgg(wizard, egg);

            return;
        }

        Timers.StartSingleTimer(
            $"eggHatch_{egg.m_globalID}",
            new CHARACTER_103_PROTOCOL.MSG_DOEGGHATCH { EggGlobalId = egg.m_globalID },
            TimeSpan.FromSeconds(remainingSeconds)
        );
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOEGGHATCH))]
    private void ReceiveEggHatch(CHARACTER_103_PROTOCOL.MSG_DOEGGHATCH message) {
        var wizard = GetActiveWizard();
        var egg = wizard.PetOwnerBehavior.MorphingSlots
            ?.FirstOrDefault(e => e.m_globalID == message.EggGlobalId);

        if (egg == null) {
            Logger.Debug("MSG_DOEGGHATCH for unknown egg {0}.", Logger.Args(message.EggGlobalId));
            return;
        }

        HatchEgg(wizard, egg);
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_HATCHEGGNOW))]
    private void ReceiveHatchEggNow(PET_9_PROTOCOL.MSG_HATCHEGGNOW message) {
        var wizard = GetActiveWizard();
        var egg = wizard.PetOwnerBehavior.MorphingSlots
            ?.FirstOrDefault(e => e.m_globalID == message.EggGID);

        if (egg == null) {
            Logger.Debug("MSG_HATCHEGGNOW for unknown egg {0}.", Logger.Args(message.EggGID));
            return;
        }

        // TODO: validate gold cost against message.Gold
        // For now, just hatch immediately.
        HatchEgg(wizard, egg);
    }

    /// <summary>
    /// Serializes a ClientPetOwnerBehavior into a ByteString for MSG_PETUPDATEBEHAVIOR.
    /// </summary>
    private static ByteString SerializeBehaviorBlob(ServerPetOwnerBehavior behavior) {
        var clientBehavior = behavior.GetClientBehaviorInstance();
        var serializer = new ObjectSerializer(
            Versionable: false,
            Behaviors: SerializerFlags.None
        );
        if (!serializer.Serialize(clientBehavior, (PropertyFlags) 31, out var blob)) {
            Logger.Error("Failed to serialize ClientPetOwnerBehavior for MSG_PETUPDATEBEHAVIOR.");

            return string.Empty;
        }

        return blob;
    }

    /// <summary>
    /// Performs the full hatch sequence: removes egg from slot, sends the four
    /// hatch notification messages to the client, and adds the pet to the tome.
    /// </summary>
    private void HatchEgg(Wizard wizard, CraftingSlot egg) {
        // Resolve the pet template from the egg's recipe name.
        if (!ServerPetOwnerBehavior.TryGetPetTemplateFromEgg(egg, out var petTemplateId)) {
            Logger.Debug("HatchEgg: could not parse pet template from egg {0}, recipe='{1}'.",
                Logger.Args(egg.m_globalID, egg.m_recipeName));
                
            return;
        }

        // Remove the egg from morphing slots and notify client.
        wizard.PetOwnerBehavior.HatchEgg(egg);

        SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHINGSLOT {
            GlobalID = egg.m_globalID,
            Removed = 1,
            ExpireTimeCount = (uint) egg.m_timeFinished
        });

        // 1. MSG_PETLEVELUP — pet goes from level 0 (egg) to level 1.
        SendToSocket(new PET_9_PROTOCOL.MSG_PETLEVELUP {
            GlobalID = egg.m_globalID,
            OverallRating = DEFAULT_PET_OVERALL_RATING,
            ActiveRating = 0,
            PetLevel = 1,
            NewTalent = 0,
            NewDerbyPower = 0,
            NewJewel = 0,
            Display = 1
        });

        // 2. MSG_PETUPDATEBEHAVIOR — updated PetOwnerBehavior blob (egg removed).
        var behaviorBlob = SerializeBehaviorBlob(wizard.PetOwnerBehavior);
        SendToSocket(new PET_9_PROTOCOL.MSG_PETUPDATEBEHAVIOR {
            GlobalID = egg.m_globalID,
            Data = behaviorBlob
        });

        // 3. MSG_PETHATCHED — tells client the egg hatched into this pet template.
        SendToSocket(new PET_9_PROTOCOL.MSG_PETHATCHED {
            GlobalID = egg.m_globalID,
            TemplateID = petTemplateId
        });

        // 4. MSG_PETTOMEPETADDED — adds the pet to the player's pet tome.
        var petTomeGlobalId = RandomGen.GenerateGUID();
        wizard.OwnedPets[petTomeGlobalId] = (uint) petTemplateId;

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_PETTOMEPETADDED {
            GlobalID = petTomeGlobalId,
            PetTemplateID = (uint) petTemplateId
        });

        Logger.Information("Pet hatched! Egg {0} → template {1} (tome entry: {2})",
            Logger.Args(egg.m_globalID, petTemplateId, petTomeGlobalId));
    }
    
}