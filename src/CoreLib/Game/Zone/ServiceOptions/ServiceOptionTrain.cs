/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common.Caches;
using System.Collections.Generic;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.ServiceOptions;

public class ServiceOptionTrain : ServiceOption {
    public override string ServiceName { get; protected set; } = "WizTrainingService";
    public override string WizBang { get; set; } = "Shopping";
    public override string NpcTextKeyOverride { get; protected set; } = "GUI_NPCInteractText";
    public override List<ServiceOptionBase> ServiceOptionBases { get; set; } = new() {
        new WizTrainingOption() {
            m_displayKey = "Spells_00000493",
            m_forceInteract = false,
            m_iconKey = "Training",
            m_serviceIndex = 0,
            m_serviceName = "WizTrainingService",

            m_bCanTrain = true,
            m_failedRequirement = null,
            m_requiredLevel = 1,
            m_requirements = null,
            m_spellName = "Thunder Snake",
            m_trainingCost = 1,
            m_trainingIndex = 0
        },
        new WizTrainingOption() {
            m_displayKey = "Spells_00000573",
            m_forceInteract = false,
            m_iconKey = "Training",
            m_serviceIndex = 0,
            m_serviceName = "WizTrainingService",

            m_bCanTrain = false,
            m_failedRequirement = new ReqHasSpell() {
                m_applyNOT = false,
                m_operator = Requirement.Operator.ROP_AND,
                m_spellName = "Thunder Snake"
            },
            m_requiredLevel = 5,
            m_requirements = new RequirementList() {
                m_applyNOT = false,
                m_operator = Requirement.Operator.ROP_AND,
                m_requirements = new List<Requirement>() {
                    new ReqHasSpell() {
                        m_applyNOT = false,
                        m_operator = Requirement.Operator.ROP_AND,
                        m_spellName = "Thunder Snake"
                    }
                }
            },
            m_spellName = "Lightning Bats",
            m_trainingCost = 1,
            m_trainingIndex = 1
        }
    };

    public ServiceOptionTrain(CoreObject ActiveGameObject) : base(ActiveGameObject) { }

    public override void OnPlayerInteraction(IActorRef suspect, int serviceIndex) {
        var dyeShopOpen = new WIZARD_12_PROTOCOL.MSG_DYESHOPOPEN() {
            GlobalID = ActiveGameObject.m_globalID,
            Title = "WC-NPCs_00000718"
        };

        suspect.Tell(dyeShopOpen);
    }
}
