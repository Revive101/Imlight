/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.World;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionTrain : ServiceOption {
    public override string ServiceName { get; protected set; } = "WizTrainingService";
    public override string WizBang { get; set; } = "Training";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_NPCInteractText";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new();

    public readonly List<NPCSpellEntry> SpellInventory = new();

    public ServiceOptionTrain(CoreObject ActiveGameObject, List<NPCSpellEntry> npcSpellInventory) : base(ActiveGameObject) {
        SpellInventory = npcSpellInventory;
        RecalculateOnProximityEnter = true;

        foreach (var spell in SpellInventory) {
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.TemplateID);

            var spellOption = new WizTrainingOption();

            spellOption.m_serviceIndex = 0;
            spellOption.m_forceInteract = false;
            spellOption.m_iconKey = "Training";
            spellOption.m_serviceName = "WizTrainingService";

            spellOption.m_spellName = spellTemplate.m_name;
            spellOption.m_displayKey = spellTemplate.m_displayName;
            spellOption.m_requiredLevel = spell.Level;
            spellOption.m_trainingIndex = npcSpellInventory.IndexOf(spell);
            spellOption.m_trainingCost = 1;

            spellOption.m_bCanTrain = false;
            spellOption.m_failedRequirement = null;

            if (spell.RequiredSpellID != 0) {
                var reqSpellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(spell.RequiredSpellID);

                spellOption.m_requirements = new RequirementList() {
                    m_applyNOT = false,
                    m_operator = Requirement.Operator.ROP_AND,
                    m_requirements = new List<Requirement>() {
                        new ReqHasSpell() {
                            m_applyNOT = false,
                            m_operator = Requirement.Operator.ROP_AND,
                            m_spellName = reqSpellTemplate.m_name,
                        }
                    }
                };
            }
            else {
                spellOption.m_requirements = null;
            }

            ServiceOptionBases.Add(spellOption);
        }
    }

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) { }

    public override List<ServiceOptionBase> Recalculate(IActorRef suspect) {
        // Get interacting wizard
        var queryCharacterMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        var wizard = suspect
            .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryCharacterMsg)
            .Result
            .Wizard;

        List<ServiceOptionBase> newServiceOptions = new List<ServiceOptionBase>();

        for (int i = 0; i < ServiceOptionBases.Count; i++) {
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(SpellInventory[i].TemplateID);

            var hasSpell = wizard.SpellbookBehavior.HasSpell((uint) SpellInventory[i].TemplateID);
            if (hasSpell) {
                continue;
            }

            var newBase = (WizTrainingOption) ServiceOptionBases[i];

            // If a wizard is the same school as the spell, it is free to train.
            var schoolName = wizard.MagicSchoolBehavior.MagicSchool.ToString();
            if (spellTemplate.m_sMagicSchoolName == schoolName) {
                newBase.m_trainingCost = 0;
            }
            else {
                newBase.m_trainingCost = 1;
            }

            // Check if wizard has enough training points to train the spell.
            if (wizard.MagicSchoolBehavior.TrainingPoints < newBase.m_trainingCost) {
                newBase.m_bCanTrain = false;
                newBase.m_failedRequirement = null;
                continue;
            }

            // Check if wizard is high enough level to train the spell.
            if (wizard.MagicSchoolBehavior.Level < SpellInventory[i].Level) {
                newBase.m_bCanTrain = false;
                newBase.m_failedRequirement = null;
                continue;
            }

            // If the spell has no required spell, it can be trained, else check if the wizard has the required spell.
            if (SpellInventory[i].RequiredSpellID == 0) {
                newBase.m_bCanTrain = true;
                newBase.m_failedRequirement = null;
            }
            else {
                var hasReqSpell = wizard.SpellbookBehavior.HasSpell((uint) SpellInventory[i].RequiredSpellID);

                if (!hasReqSpell) {
                    var reqSpellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(SpellInventory[i].RequiredSpellID);

                    newBase.m_bCanTrain = false;
                    newBase.m_failedRequirement = new ReqHasSpell() {
                        m_applyNOT = false,
                        m_operator = Requirement.Operator.ROP_AND,
                        m_spellName = reqSpellTemplate.m_name
                    };
                }
                else {
                    newBase.m_bCanTrain = true;
                    newBase.m_failedRequirement = null;
                }
            }

            newServiceOptions.Add(newBase);
        }

        return newServiceOptions;
    }
}
