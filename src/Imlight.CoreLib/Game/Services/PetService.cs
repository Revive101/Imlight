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
 * Last Updated: 10/25/2026
 */

using System;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Behaviors;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Utilities;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using AdvancedPetsConfig = Imlight.CoreLib.Game.Pet.Config.AdvancedPetsConfig;

namespace Imlight.CoreLib.Game.Services;

internal class PetService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const int PET_ENERGY_TICK_DELAY = 2;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new PetService(parentActor));

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;
        var clientMaxEnergy = wizard.GameStats.GetClientTypeAlternative().m_energyMax;

        var tickTime = petOwnerBehavior.LastEnergyTickEpoch;
        if (tickTime <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
            tickTime = (uint) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + AdvancedPetsConfig.EnergyTickInSeconds);
        }

        var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
            GlobalID = wizard.GameObjectID,
            Energy = petOwnerBehavior.Energy,
            MaxEnergy = clientMaxEnergy,
            TickTime = (int) tickTime
        };

        SendToSocket(tickMsg);

        var tickTimeSeconds = tickTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var firstTickDelay = TimeSpan.FromSeconds(tickTimeSeconds + PET_ENERGY_TICK_DELAY);
        var tickInterval = TimeSpan.FromSeconds(AdvancedPetsConfig.EnergyTickInSeconds + PET_ENERGY_TICK_DELAY);
        Timers.StartPeriodicTimer("petEnergyTick", new CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK(), firstTickDelay, tickInterval);

        InitializeEggs(wizard);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK))]
    private void ReceiveDoEnergyTick(CHARACTER_103_PROTOCOL.MSG_DOENERGYTICK message) {
        var wizard = GetActiveWizard();
        var petOwnerBehavior = wizard.PetOwnerBehavior;
        var maxEnergy = wizard.GameStats.m_energyMax;
        var clientMaxEnergy = wizard.GameStats.GetClientTypeAlternative().m_energyMax;

        var tickTime = (int) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + AdvancedPetsConfig.EnergyTickInSeconds);

        if (petOwnerBehavior.Energy < maxEnergy) {
            wizard.UpdateEnergy(petOwnerBehavior.Energy + 1);
            var tickMsg = new PET_9_PROTOCOL.MSG_PETENERGYTICK() {
                GlobalID = wizard.GameObjectID,
                Energy = petOwnerBehavior.Energy,
                MaxEnergy = clientMaxEnergy,
                TickTime = tickTime
            };

            SendToSocket(tickMsg);
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_DOEGGHATCH))]
    private void ReceiveEggHatch(CHARACTER_103_PROTOCOL.MSG_DOEGGHATCH message) {
        var wizard = GetActiveWizard();
        var egg = wizard.PetOwnerBehavior.MorphingSlots
            ?.FirstOrDefault(e => e.m_globalID == message.EggGlobalId);

        if (egg == null) {
            Logger.Debug("A hatch request named egg {EggId}, which the character does not have.", Logger.Args(message.EggGlobalId));
            return;
        }

        HatchEgg(wizard, egg);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_PETEGGLAID))]
    private void ReceivePetEggLaid(CHARACTER_103_PROTOCOL.MSG_PETEGGLAID message) {
        var egg = GetActiveWizard().PetOwnerBehavior.MorphingSlots
            ?.FirstOrDefault(e => e.m_globalID == message.EggGlobalId);
        if (egg is null) {
            Logger.Warning("The laid egg {EggId} is not in a morphing slot.", Logger.Args(message.EggGlobalId));

            return;
        }

        SendEggCreatedMessages(egg);
        StartEggTimer(egg);
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_HATCHEGGNOW))]
    private void ReceiveHatchEggNow(PET_9_PROTOCOL.MSG_HATCHEGGNOW message) {
        var wizard = GetActiveWizard();
        var egg = wizard.PetOwnerBehavior.MorphingSlots
            ?.FirstOrDefault(e => e.m_globalID == message.EggGID);

        if (egg == null) {
            Logger.Debug("A hatch-now request named egg {EggId}, which the character does not have.", Logger.Args(message.EggGID));
            return;
        }

        // TODO: validate gold cost against message.Gold
        // For now, just hatch immediately.
        HatchEgg(wizard, egg);
    }

    [MessageHandler(typeof(WIZARD2_53_PROTOCOL.MSG_PETFEEDHAPPINESS))]
    private void ReceivePetFeedHappiness(WIZARD2_53_PROTOCOL.MSG_PETFEEDHAPPINESS message) {
        var wizard = GetActiveWizard();
        var pet = wizard.GetEquippedPetItem();
        if (!PetProgression.TryGetPet(pet, out var behavior, out _) || behavior.m_level == 0) {
            Logger.Debug("MSG_PETFEEDHAPPINESS without a hatched pet equipped.");

            return;
        }

        var result = PetFeeding.Feed(wizard, pet, message.SnackTemplateID, message.FeedStack != 0);
        if (PetFeeding.TryCreateSnackBagUpdate(wizard, result, out var bagUpdate)) {
            SendToSocket(bagUpdate);
        }

        // The client takes this as the pet's happiness, so a refused feed answers with it unchanged.
        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_PETFEEDHAPPINESS { Happiness = behavior.m_happiness });
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
        if (!ServerPetOwnerBehavior.TryGetPetTemplateFromEgg(egg, out var petTemplateId)) {
            Logger.Warning("The egg {EggId} names no pet template, so the client is not told about it.", Logger.Args(egg.m_globalID.Full));

            return;
        }

        var eggItem = GetActiveWizard().FindPetItem(egg.m_globalID);
        ClientPetNameBehavior eggName = null;
        if (eggItem is not null) {
            CoreObjectFactory.FindBehaviorInstance(eggItem, out eggName);
        }

        SendToSocket(new PET_9_PROTOCOL.MSG_PETEGGMORPHED {
            PetTemplateGID = petTemplateId,
            PetName = eggName?.m_nameKeys ?? 0,
            HatchTime = (uint) Math.Max(0, egg.m_timeFinished - DateTimeOffset.UtcNow.ToUnixTimeSeconds())
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

    private void HatchEgg(Wizard wizard, CraftingSlot egg) {
        // Resolve the pet template from the egg's recipe name.
        if (!ServerPetOwnerBehavior.TryGetPetTemplateFromEgg(egg, out var petTemplateId)) {
            Logger.Debug("Egg {EggId} names recipe '{Recipe}', which has no pet template, so it does not hatch.",
                Logger.Args(egg.m_globalID, egg.m_recipeName));

            return;
        }

        // Remove the egg from morphing slots and notify client.
        wizard.PetOwnerBehavior.HatchEgg(egg);
        TellOtherServices(new CHARACTER_103_PROTOCOL.MSG_BADGEEVENT { Kind = BadgeEventKind.Recheck });

        WizardCollection.UpdateCharacterPetOwnerBehavior(wizard);

        SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHINGSLOT {
            GlobalID = egg.m_globalID,
            Removed = 1,
            ExpireTimeCount = (uint) egg.m_timeFinished
        });

        // The egg and the pet item share a global id.
        var pet = wizard.FindPetItem(egg.m_globalID);
        ClientPetItemBehavior petBehavior = null;
        if (pet is not null && PetProgression.Hatch(pet, (uint) Math.Min(egg.m_timeFinished, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))) {
            wizard.SavePet(pet);
            CoreObjectFactory.FindBehaviorInstance(pet, out petBehavior);
        } else {
            Logger.Warning("Egg {EggId} hatched, but the wizard has no pet item with that id.", Logger.Args(egg.m_globalID.Full));
            pet = null;
        }

        // 1. MSG_PETLEVELUP — pet goes from level 0 (egg) to level 1.
        SendToSocket(new PET_9_PROTOCOL.MSG_PETLEVELUP {
            GlobalID = egg.m_globalID,
            OverallRating = (byte) (petBehavior?.m_overallRating ?? 0),
            ActiveRating = (byte) (petBehavior?.m_activeRating ?? 0),
            PetLevel = 1,
            NewTalent = 0,
            NewDerbyPower = 0,
            NewJewel = 0,
            Display = 1
        });

        // 2. MSG_PETUPDATEBEHAVIOR: the pet item's state, keyed by the egg gid, which is the pet item gid.
        if (PetBehaviorUpdate.TryCreate(pet, out var behaviorUpdate)) {
            SendToSocket(behaviorUpdate);
        }

        // 3. MSG_PETHATCHED — tells client the egg hatched into this pet template.
        SendToSocket(new PET_9_PROTOCOL.MSG_PETHATCHED {
            GlobalID = egg.m_globalID,
            TemplateID = petTemplateId
        });

        // 4. MSG_PETTOMEPETADDED — adds the pet to the player's pet tome.
        var petTomeGlobalId = RandomGen.GenerateGUID();

        SendToSocket(new WIZARD2_53_PROTOCOL.MSG_PETTOMEPETADDED {
            GlobalID = petTomeGlobalId,
            PetTemplateID = (uint) petTemplateId
        });

        Logger.Debug("{Wizard} hatched egg {EggId} into pet template {TemplateId} (tome entry {TomeEntryId}).",
            Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), egg.m_globalID, petTemplateId, petTomeGlobalId));
    }

}
