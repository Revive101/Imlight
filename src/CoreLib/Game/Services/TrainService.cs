/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Akka.Actor;
using Imlight.Common.Caches;
using Imlight.CoreLib.Game.Zone;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Game.Zone.ServiceOptions;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Services;

internal class TrainService : MessageService {
    public TrainService(SessionActor sessionActor) : base(sessionActor) { }

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new TrainService(parentActor));

    [MessageHandler(typeof(WIZARD_12_PROTOCOL.MSG_TRAIN))]
    private void ReceiveTrain(WIZARD_12_PROTOCOL.MSG_TRAIN message) {
        // Query the zone for the NPC by the ID
        var msg = new ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECT() {
            GlobalID = message.MobileID
        };
        var response = AskOtherService<ZONE_102_PROTOCOL.MSG_QUERYZONEOBJECTRSP>(msg);
        if (response is null) {
            return;
        }

        var wizard = GetActiveWizard();
        var npcObj = (WizardZoneNpc) response.ZoneObject;

        var serviceOption = (ServiceOptionTrain) npcObj.ServiceOptions.FirstOrDefault(x => x.ServiceName == "WizTrainingService");
        var spellEntry = serviceOption.SpellInventory[message.TrainingIndex];
        var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spellEntry.TemplateID);
        var spellCost = wizard.MagicSchoolBehavior.MagicSchool.ToString() == spellTemplate.m_sMagicSchoolName ? 0 : 1;

        var spell = SpellFactory.CreateSpellFromTemplate((uint) spellEntry.TemplateID);
        wizard.LearnSpell(spell);


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
