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
 * TRAIN SERVICE
 * ========================================================================
 * 
 * PURPOSE:
 * Manages player spell training mechanics, including spell learning 
 * and training point management.
 * 
 * USAGE EXAMPLE:
 * Internal service handling spell training interactions within 
 * the game server session.
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Joji
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.Common;
using Imlight.CoreLib.Game.Badges;
using Imlight.CoreLib.Game.Zone.Components;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Game.Spells;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty.TypeCache;

namespace Imlight.CoreLib.Game.Services;

internal class TrainService(SessionActor sessionActor) : MessageService(sessionActor) {
    
    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TrainService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRAIN))]
    private void ReceiveTrain(WIZARD_12_PROTOCOL.MSG_TRAIN message) {
        // Query the zone for the NPC by the ID
        var msg = new ZONE_102_PROTOCOL.MSG_QUERYZONEENTITY() {
            GlobalID = message.MobileID
        };
        var response = AskOtherService<ZONE_102_PROTOCOL.MSG_QUERYZONEENTITYRSP>(msg);
        if (response is null || response.ZoneObject is null) {
            var wizardName = GetActiveWizard().PlayerNameBehavior.GetWizardName();
            Logger.Warning("{Wizard} asked to train with NPC {MobileId}, which is not in the zone.",
                Logger.Args(wizardName, message.MobileID));

            return;
        }

        // Ensure that the interacted object has a TrainerComponent.
        var trainerComponent = response.ZoneObject.GetComponentOfType<InteractTrainerComponent>();
        if (trainerComponent is null) {
            Logger.Warning("NPC {Npc} is not a trainer.", Logger.Args(response.ZoneObject.ActiveGameObject.m_debugName));

            return;
        }

        // We don't need to inform the TrainerComponent of our interaction, as the client
        // doesn't even send an interaction message upon training.
        var wizard = GetActiveWizard();

        // Ensure that the training index is within the bounds of the spell inventory.
        if (message.TrainingIndex < 0 || message.TrainingIndex >= trainerComponent.SpellInventory.Count) {
            Logger.Warning("{Wizard} tried to train spell {Index} from a list that is shorter.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), message.TrainingIndex));

            return;
        }

        var spellEntry = trainerComponent.SpellInventory[message.TrainingIndex];
        var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spellEntry.TemplateID);

        if (BadgeGate.Blocks(spellTemplate?.m_purchaseRequirements, spellEntry.TemplateID, SessionActor.ActorRef, GetActiveGameObject(), wizard, SessionActor.GetZoneActor())) {
            Logger.Warning("{Wizard} tried to train spell {SpellId} without the badge it requires.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), spellEntry.TemplateID));

            return;
        }

        // Determine if the wizard has enough training points to learn the spell.
        // Wizards that are of the same magic school as the spell they are training have a cost of 0.
        var spellCost = wizard.MagicSchoolBehavior.MagicSchool.ToString() == spellTemplate.m_sMagicSchoolName ? 0 : 1;
        if (wizard.MagicSchoolBehavior.TrainingPoints < spellCost) {
            Logger.Warning("{Wizard} tried to train spell {SpellId} without enough training points.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), spellEntry.TemplateID));

            return;
        }

        var spell = SpellFactory.GetSpell((uint) spellEntry.TemplateID);
        var spellLearnedSuccess = wizard.LearnSpell(spell);
        if (!spellLearnedSuccess) {
            Logger.Error("{Wizard} could not learn spell {SpellId} from a trainer.",
                Logger.Args(wizard.PlayerNameBehavior.GetWizardName(), spellEntry.TemplateID));

            return;
        }

        var addSpellMsg = new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK() {
            SpellID = (int) spellEntry.TemplateID
        };
        SendToSocket(addSpellMsg);

        var newTrainingPoints = wizard.MagicSchoolBehavior.TrainingPoints - spellCost;
        wizard.UpdateTrainingPoints(newTrainingPoints);

        var updateTrainingMsg = new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING() {
            TrainingPoints = newTrainingPoints
        };
        SendToSocket(updateTrainingMsg);

        var trainCompleteMsg = new WIZARD_12_PROTOCOL.MSG_SPELLTRAINCOMPLETE() {
            SpellID = spellEntry.TemplateID,
            DisplayText = "WizTraining_00000040",
            Success = 1
        };
        SendToSocket(trainCompleteMsg);
    }

}
