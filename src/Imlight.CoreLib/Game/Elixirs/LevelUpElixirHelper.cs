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
 * LEVEL UP & WORLD ELIXIR HELPER
 * ========================================================================
 * 
 * PURPOSE:
 * Centralizes the application of LevelUpElixirs and WorldElixirs (skipping,
 * level setting, training points, potion upgrades, spell learning, quest completion,
 * and starter gear provisioning).
 * 
 * ========================================================================
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.Cryptography;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Game.Spells;
using Imlight.CoreLib.Shared.Character;
using Imlight.CoreLib.Shared.Items;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Elixirs;

internal static class LevelUpElixirHelper {

    private static readonly CoreObjectSerializer s_coreObjectSerializer = new(behaviors: SerializerFlags.None);
    private static readonly ObjectSerializer s_itemSerializer = new(Versionable: false, Behaviors: SerializerFlags.None);

    /// <summary>
    /// Applies the effects of a LevelUpElixir or WorldElixir to the specified wizard.
    /// </summary>
    public static void ApplyElixir(Wizard wizard,
                                   LevelUpElixirBehaviorTemplate elixirTemplate,
                                   Action<IMessage> sendToSocket,
                                   Action<IMessage, bool, ZoneBroadcastTarget> zoneBroadcast,
                                   Action<string, bool, bool, ulong, string> teleportAction,
                                   Action<string> playSound) {
        if (wizard == null || elixirTemplate == null) {
            return;
        }

        // 1. Advance Level and Base Stats if target level is greater than current level
        if (elixirTemplate.m_setCharacterToLevel > wizard.MagicSchoolBehavior.Level) {
            var targetLevel = (byte) elixirTemplate.m_setCharacterToLevel;
            if (wizard.SetLevel(targetLevel)) {
                // Broadcast level up visual effect
                var levelUpMessage = new WIZARD_12_PROTOCOL.MSG_LEVELUP {
                    GlobalID = wizard.GameObjectID,
                    NewLevel = wizard.MagicSchoolBehavior.Level,
                    Data = "0000000000"
                };
                zoneBroadcast(levelUpMessage, false, ZoneBroadcastTarget.All);

                var school = wizard.MagicSchoolBehavior.MagicSchool;
                var baseStats = MagicLevelsConfig.GetPlayerLevelInfo(school, targetLevel);

                // Heal to max
                wizard.UpdateHealth(wizard.GameStats.m_baseHitpoints);
                sendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEHEALTH {
                    CharacterID = wizard.GameObjectID,
                    NewHealth = baseStats.m_hitpoints,
                    NewHealthMax = baseStats.m_hitpoints,
                    DisplayDiff = 1
                });

                // Mana refill
                wizard.UpdateMana(wizard.GameStats.m_baseMana);
                sendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEMANA {
                    Mana = baseStats.m_mana,
                    MaxMana = baseStats.m_mana,
                    DisplayDiff = 1
                });

                // Power pips
                sendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEPOWERPIP {
                    PowerPip = baseStats.m_pipChance
                });

                // Energy max
                wizard.UpdateEnergy(baseStats.m_petEnergy);
                sendToSocket(new PET_9_PROTOCOL.MSG_PETENERGYMAX {
                    MaxEnergy = baseStats.m_petEnergy
                });
            }
        }

        // 2. Training Points
        if (elixirTemplate.m_resultingTrainingPoints > 0) {
            var newTrainingPoints = Math.Max(wizard.MagicSchoolBehavior.TrainingPoints, elixirTemplate.m_resultingTrainingPoints);
            wizard.UpdateTrainingPoints(newTrainingPoints);
            sendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATETRAINING {
                TrainingPoints = newTrainingPoints
            });
        }

        // 3. Gold
        if (elixirTemplate.m_gold > 0) {
            wizard.AddGold(elixirTemplate.m_gold);
            sendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
                Gold = wizard.GameStats.m_currentGold,
                MaxGold = wizard.GameStats.m_baseGoldPouch
            });
        }

        // 4. Potions
        if (elixirTemplate.m_maxPotions > 0) {
            var newMaxPotions = Math.Max((int) wizard.GameStats.m_potionMax, elixirTemplate.m_maxPotions);
            wizard.UpdatePotions(newMaxPotions, newMaxPotions);
            sendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEPOTIONS {
                PotionCharge = newMaxPotions,
                PotionMax = newMaxPotions
            });
        }

        // 5. Complete World Quests if WorldElixirBehaviorTemplate
        if (elixirTemplate is WorldElixirBehaviorTemplate worldElixir && !string.IsNullOrWhiteSpace(worldElixir.m_questPrefix)) {
            var prefix = worldElixir.m_questPrefix.ToString().Trim();
            var allQuests = QuestTemplateCollection.GetAllQuests();

            foreach (var qTemplate in allQuests) {
                if (qTemplate != null && !string.IsNullOrEmpty(qTemplate.m_questName) && qTemplate.m_questName.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                    CompleteQuestForWizard(wizard, qTemplate.m_questName, sendToSocket);
                }
            }
        }

        // 6. Apply School-Specific & All-School Data
        var schoolDataList = new List<LevelUpElixirSchoolSpecificData>();
        if (elixirTemplate.m_allSchoolData != null) {
            schoolDataList.Add(elixirTemplate.m_allSchoolData);
        }

        if (elixirTemplate.m_schoolSpecificData != null) {
            var playerSchoolName = wizard.MagicSchoolBehavior.MagicSchool.ToString();
            var matchingSchool = elixirTemplate.m_schoolSpecificData.FirstOrDefault(s =>
                string.Equals(s.m_schoolName, playerSchoolName, StringComparison.OrdinalIgnoreCase));

            if (matchingSchool != null) {
                schoolDataList.Add(matchingSchool);
            }
        }

        string teleportDestination = null;

        foreach (var data in schoolDataList) {
            // A. Complete specific quests
            if (data.m_questsToComplete != null) {
                foreach (var questName in data.m_questsToComplete) {
                    if (!string.IsNullOrWhiteSpace(questName)) {
                        CompleteQuestForWizard(wizard, questName, sendToSocket);
                    }
                }
            }

            // B. Add new entry quests
            if (data.m_questsToAdd != null) {
                foreach (var questName in data.m_questsToAdd) {
                    if (!string.IsNullOrWhiteSpace(questName) && !wizard.QuestBehavior.HasQuest(questName) && !wizard.QuestBehavior.HasCompletedQuest(questName)) {
                        var questTemplate = QuestTemplateCollection.GetQuestByName(questName);
                        if (questTemplate != null) {
                            var newInstance = new QuestInstance(questTemplate, wizard.CharId);
                            if (wizard.AddQuest(newInstance)) {
                                SendQuestStartingMessage(newInstance, questTemplate, sendToSocket);
                            }
                        }
                    }
                }
            }

            // C. Spells to Learn
            if (data.m_spellsToGive != null) {
                foreach (var spellName in data.m_spellsToGive) {
                    if (string.IsNullOrWhiteSpace(spellName)) {
                        continue;
                    }

                    var spell = SpellFactory.GetSpell(spellName);
                    if (spell != null && wizard.LearnSpell(spell)) {
                        sendToSocket(new WIZARD_12_PROTOCOL.MSG_ADDSPELLTOBOOK {
                            SpellID = (int) spell.m_templateID
                        });
                    }
                }
            }

            // D. Items to Place in Inventory
            if (data.m_itemsToPlaceInInventory != null) {
                foreach (var itemTemplateId in data.m_itemsToPlaceInInventory) {
                    if (itemTemplateId == 0) {
                        continue;
                    }

                    if (wizard.AddItemToInventory(itemTemplateId, out var itemCoreObject)) {
                        if (s_coreObjectSerializer.Serialize(itemCoreObject, 24, out var serializedItem)) {
                            sendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                                GlobalID = wizard.GameObjectID,
                                SerializedItem = serializedItem
                            });
                        }
                    }
                }
            }

            // E. Gear to Equip
            if (data.m_gearToEquip != null) {
                foreach (var gearTemplateId in data.m_gearToEquip) {
                    if (gearTemplateId == 0) {
                        continue;
                    }

                    if (wizard.AddItemToInventory(gearTemplateId, out var gearCoreObject)) {
                        if (s_coreObjectSerializer.Serialize(gearCoreObject, 24, out var serializedGear)) {
                            sendToSocket(new GAME_5_PROTOCOL.MSG_INVENTORYBEHAVIOR_ADDITEM {
                                GlobalID = wizard.GameObjectID,
                                SerializedItem = serializedGear
                            });
                        }

                        // Equip the item
                        var gearTemplate = CoreObjectFactory.GetCoreTemplate(gearTemplateId) as WizItemTemplate;
                        if (gearTemplate != null) {
                            var equipSlot = ItemHelper.GetItemSlot(gearTemplate);
                            if (equipSlot != null) {
                                var slotName = equipSlot.SlotType.ToString().Split('.')[^1];
                                if (wizard.InventoryToEquipmentTransfer(gearCoreObject.m_globalID, out _, out _)) {
                                    sendToSocket(new GAME_5_PROTOCOL.MSG_EQUIPITEM {
                                        ItemID = gearCoreObject.m_globalID,
                                        SlotName = slotName,
                                        IsEquip = 1
                                    });

                                    // Broadcast public equip
                                    var pubItem = ItemHelper.GetPublicItem(gearCoreObject);
                                    if (s_itemSerializer.Serialize(pubItem, 1, out var pubData)) {
                                        zoneBroadcast(new GAME_5_PROTOCOL.MSG_EQUIPMENTBEHAVIOR_PUBLICEQUIPITEM {
                                            GlobalID = wizard.GameObjectID,
                                            SerializedInfo = pubData
                                        }, false, ZoneBroadcastTarget.All);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // F. Set destination zone if specified
            if (!string.IsNullOrWhiteSpace(data.m_teleportToZoneOnComplete)) {
                teleportDestination = data.m_teleportToZoneOnComplete;
            }
        }

        // 7. Equip Sound
        if (!string.IsNullOrWhiteSpace(elixirTemplate.m_equipSound)) {
            playSound?.Invoke(elixirTemplate.m_equipSound);
        }

        // 8. Teleport if a destination was configured
        if (!string.IsNullOrWhiteSpace(teleportDestination) && teleportAction != null) {
            teleportAction(teleportDestination, true, false, wizard.CharId, "Start");
        }
    }

    private static void CompleteQuestForWizard(Wizard wizard, string questName, Action<IMessage> sendToSocket) {
        var activeInstance = wizard.QuestBehavior.CurrentQuestInstances.FirstOrDefault(q =>
            string.Equals(q?.QuestName, questName, StringComparison.OrdinalIgnoreCase));

        if (activeInstance != null) {
            var questId = activeInstance.ID;
            wizard.CompleteQuest(questName);

            sendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_COMPLETEQUEST {
                QuestID = questId
            });
            sendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_REMOVEQUEST {
                QuestID = questId
            });
        }
        else if (!wizard.QuestBehavior.HasCompletedQuest(questName)) {
            wizard.QuestBehavior.AddToQuestRegistry(questName, "Complete", 1);
            WizardCollection.UpdateCharacterQuestBehavior(wizard);
        }
    }

    private static void SendQuestStartingMessage(QuestInstance questInstance, QuestTemplate qTemplate, Action<IMessage> sendToSocket) {
        var goalSerializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);

        var qMadLibs = QuestMadlibs.GetMadLibForQuest(qTemplate);
        if (!goalSerializer.Serialize(qMadLibs, 1, out var madLibData)) {
            return;
        }

        sendToSocket(new QUEST_MESSAGES_52_PROTOCOL.MSG_SENDQUEST {
            QuestID = questInstance.ID,
            QuestNameID = StringHash.Compute(qTemplate.m_questName),
            QuestType = 0,
            QuestLevel = qTemplate.m_questLevel,
            QuestTitle = qTemplate.m_questTitle,
            QuestInfo = "",
            New = 1,
            QuestMadlibs = madLibData,
            GoalData = "",
            Rewards = "",
            ClientTags = "",
            AssociatedWorlds = "",
            NoQuestHelper = qTemplate.m_noQuestHelper ? (byte) 1 : (byte) 0,
            Mainline = qTemplate.m_mainline ? (byte) 1 : (byte) 0,
            ReadyToTurnIn = 0,
            SkipQHAutoSelect = 0,
            PetOnlyQuest = qTemplate.m_playAsYourPetNPC ? (byte) 1 : (byte) 0,
            ActivityType = 0
        });
    }

}
