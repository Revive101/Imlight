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
 * INTERACT TRAINER
 * ========================================================================
 * 
 * PURPOSE:
 * Manages spell training interactions for NPCs, dynamically generating 
 * spell training options based on player characteristics and spell requirements.
 * 
 * USAGE EXAMPLE:
 * 
 * NOTE:
 * Dynamically resolves spell training options from NPC spell inventory.
 * Considers player's magic school, level, and existing spellbook.
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.WizBang;
using Imlight.CoreLib.Game.Zone.Core;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Imlight.CoreLib.WizardData.Models.World;

namespace Imlight.CoreLib.Game.Zone.Components;

internal sealed class InteractTrainerComponent(ZoneEntity entity) : ZoneEntityComponent(entity), IServiceComponent, IComponentFactory {

    public string ServiceName     => "WizTrainingService";
    public string NpcIcon         => null;
    public string NpcNameKey      => null;
    public string NpcTextKey      => "GUI_NPCInteractText";
    public WizBangs WizBang       => WizBangs.Training;
    public string StateName       => "Shop";
    public string InteractWizBang => "Registrar";
    public string DisplayKey      => "GUI_ShopOptionEquipment";

    public List<NPCSpellEntry> SpellInventory { get; private set; } = [];
    private List<ServiceOptionBase> _serviceOptionBases = [];

    public static bool ShouldAttachToEntity(CoreTemplate template) 
        // Attach if the template is an NPC and has a spell inventory in Dragon database.
        => template is GameObjectTemplate goTemplate 
        && goTemplate.m_behaviors.Any(x => x is NPCBehaviorTemplate) 
        && NpcSpellInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out _);

    public override void OnStart() {
        // Search Dragon database for our spell inventory.
        if (    Entity.Template is not GameObjectTemplate goTemplate
            || !NpcSpellInventoryCollection.TryGetNpcInventory(goTemplate.m_templateID, out var spellInventory)) {
            Logger.Error("{0} on entity {1} has no spell inventory.", 
                Logger.Args(ServiceName, Entity.ActiveGameObject.m_debugName));

            return;
        }

        SpellInventory = spellInventory.Spells;
        InitializeServiceBases();
    }

    public IEnumerable<ServiceOptionBase> GetServiceOptions(Wizard wizard) {
        if (wizard is null) {
            return _serviceOptionBases;
        }

        // We've initialized a list of service option bases in InitializeServiceBases.
        // However, these options can change depending on how far the player has progressed.
        // We must now edit each service option to reflect the player's status.
        List<ServiceOptionBase> newServiceOptions = [];

        for (int i = 0; i < _serviceOptionBases.Count; i++) {
            var spellTemplate = (SpellTemplate) CoreObjectFactory.GetCoreTemplate(SpellInventory[i].TemplateID);

            var hasSpell = wizard.SpellbookBehavior.HasSpell((uint) SpellInventory[i].TemplateID);
            if (hasSpell) {
                continue;
            }

            var newBase = (WizTrainingOption) _serviceOptionBases[i];

            // If a wizard is the same school as the spell, it is free to train.
            var schoolName = wizard.MagicSchoolBehavior.MagicSchool.ToString();
            if (spellTemplate.m_sMagicSchoolName == schoolName) {
                newBase.m_trainingCost = 0;
            }
            else {
                newBase.m_trainingCost = 1;
            }

            // Check if wizard has enough training points to train the spell, and if the
            // wizard is high enough level to train the spell.
            if (wizard.MagicSchoolBehavior.TrainingPoints < newBase.m_trainingCost
                    || wizard.MagicSchoolBehavior.Level < SpellInventory[i].Level) {
                newBase.m_bCanTrain = false;
                newBase.m_failedRequirement = null;

                newServiceOptions.Add(newBase);
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
                        m_operator = Operator.ROP_AND,
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

    public void OnServiceInteraction(IActorRef playerActor, Wizard playerCharacter, CoreObject playerObject, uint serviceOptionIndex) { }

    private void InitializeServiceBases() {
        // Foreach spell in the inventory, create a service option. This method serves to initialize defaults.
        // When a player interacts with this object, we'll go back and edit each service option
        // depending on the player's current level, school, and existing spell inventory.
        _serviceOptionBases = [];

        foreach (var spell in SpellInventory) {
            var template = CoreObjectFactory.GetCoreTemplate(spell.TemplateID);
            if (template is null || template is not SpellTemplate spellTemplate) {
                Logger.Error("Trainer {0} has an invalid spell template with ID {1}",
                    Logger.Args(Entity.ActiveGameObject.m_templateID, spell.TemplateID));

                continue;
            }

            var spellOption = new WizTrainingOption {
                m_iconKey = WizBang.ToString(),
                m_serviceName = ServiceName,

                m_spellName = spellTemplate.m_name,
                m_displayKey = spellTemplate.m_displayName,
                m_requiredLevel = spell.Level,
                m_trainingIndex = SpellInventory.IndexOf(spell),
                m_trainingCost = spellTemplate.m_trainingCost,
            };

            if (spell.RequiredSpellID != 0) {
                var reqTemplate = CoreObjectFactory.GetCoreTemplate(spell.RequiredSpellID);
                if (reqTemplate is null || reqTemplate is not SpellTemplate reqSpellTemplate) {
                    Logger.Error("Trainer {0} has an invalid required spell template with ID {1}",
                        Logger.Args(Entity.ActiveGameObject.m_templateID, spell.RequiredSpellID));

                    continue;
                }

                spellOption.m_requirements = new RequirementList() {
                    m_applyNOT = false,
                    m_operator = Operator.ROP_AND,
                    m_requirements = new List<Requirement>() {
                        new ReqHasSpell() {
                            m_applyNOT = false,
                            m_operator = Operator.ROP_AND,
                            m_spellName = reqSpellTemplate.m_name,
                        }
                    }
                };
            }
            else {
                spellOption.m_requirements = null;
            }

            _serviceOptionBases.Add(spellOption);
        }
    }

}