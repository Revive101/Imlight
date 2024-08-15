/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Models.Player;
using static Imlight.Common.Caches.TypeCache;

namespace Imlight.CoreLib.Game.Zone.NPC;

/// <summary>
/// This is a zone NPC which manages itself as an actor.
/// </summary>
public class WizardZoneNpc : WizardZoneObject {

    public bool IsSpellTrainer { get; set; }
    public ServiceMementoBase ServiceMomentoBase { get; private set; }

    protected readonly ObjectSerializer _serializer = new ObjectSerializer()
            .OnBehaviors(SerializerOptions.Behaviors.None)
            .OnPropertyMask((SerializerOptions.PropertyFlags) 4);
    protected readonly string _npcNameKey = "NPCFormats_Name";
    protected MadlibBlock _madlibBlock;
    protected bool _turnTowardsPlayer;

    // ctor
    public WizardZoneNpc(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        : base(activeGameObject, template, wizardZoneRef) {
        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        SetMadLibBlock();
        SetServiceMomentoBase();

        // Check to see if we're a shopkeeper. If we are, set the shopkeeper properties.
        // For some reason, dye shops are not included in the world vendor locations.
        var npcName = gameObjTemplate.m_objectName.ToString().ToLower();
        if (npcName == "wc-rav-npc05") {
            SetSpellTrainer();
        }
    }

    // Akka.NET ctor
    public static Props Props(CoreObject activeGameObject, CoreTemplate template, IActorRef wizardZoneRef)
        => Akka.Actor.Props.Create(() => new WizardZoneNpc(activeGameObject, template, wizardZoneRef));

    protected override void OnPlayerJoin(CoreObject player, IActorRef suspect, Wizard wizard) {
        base.OnPlayerJoin(player, suspect, wizard);

        Sender.Tell(new ZONE_102_PROTOCOL.MSG_ADDOBJECTRSP());
    }

    protected override void OnPlayerInteractionEnter(CoreObject player, IActorRef suspect) { }

    protected override void OnPlayerInteractionExit(CoreObject player, IActorRef suspect) {
        base.OnPlayerInteractionExit(player, suspect);

        if (Template is not GameObjectTemplate gameObjTemplate) {
            return;
        }

        var leaveServiceRangeMsg = new GAME_5_PROTOCOL.MSG_LEAVESERVICERANGE {
            MobileID = ActiveGameObject.m_globalID
        };
        suspect.Tell(leaveServiceRangeMsg);
    }

    protected void SetMadLibBlock() {
        var gameObjTemplate = Template as GameObjectTemplate;
        if (gameObjTemplate is null) {
            return;
        }

        var madlibList = new List<MadlibArg> {
            new MadlibArgT_std_string() {
                m_madlibArgument = gameObjTemplate.m_displayName,
                m_madlibToken = "NAME"
            },
        };

        _madlibBlock = new MadlibBlock() {
            m_blockToken = "NPC",
            m_madlibs = madlibList
        };
    }

    protected void SetServiceMomentoBase() {
        var gameObjTemplate = Template as GameObjectTemplate;

        ServiceMomentoBase = new ServiceMementoBase() {
            m_bTurnPlayerToFace = _turnTowardsPlayer,
            m_clickToInteractOnly = false,
            m_npcFarewellSound = "",
            m_npcGreetingSound = "",
            m_npcIcon = gameObjTemplate.m_sIcon,
            m_npcNameKey = _npcNameKey,
            m_npcTextKey = "GUI_NPCInteractText",
            m_personaMadlibs = _madlibBlock,
            m_serviceOptions = new List<ServiceOptionBase>()
        };
    }

    private void SetSpellTrainer() {
        IsSpellTrainer = true;
        var gameObjTemplate = Template as GameObjectTemplate;

        if (Template.m_behaviors.FirstOrDefault(x => x is NPCBehaviorTemplate) is NPCBehaviorTemplate npcBehavior) {
            _turnTowardsPlayer = npcBehavior.m_turnTowardsPlayer;
        }
        else {
            Logger.Error("NPC {0} is a trainer but has no NPCBehaviorTemplate", Logger.Args(ActiveGameObject.m_debugName));
        }

        /*var wizTrainingService = new WizTrainingInteraction() {
            m_displayKey = "GUI_Training",
            m_forceInteract = false,
            m_iconKey = "Training",
            m_serviceIndex = 0,
            m_serviceName = "WizTrainingService",
        };
        ServiceMomentoBase.m_serviceOptions.Add(wizTrainingService);*/

        var wizTrainingOption1 = new WizTrainingOption() {
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
        };
        ServiceMomentoBase.m_serviceOptions.Add(wizTrainingOption1);

        var wizTrainingOption2 = new WizTrainingOption() {
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
        };
        ServiceMomentoBase.m_serviceOptions.Add(wizTrainingOption2);
    }
}
