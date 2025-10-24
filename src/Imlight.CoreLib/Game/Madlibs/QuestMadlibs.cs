/* 
 * Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 *
 * ========================================================================
 * QUEST MADLIBS
 * ========================================================================
 * 
 * PURPOSE:
 * Manages "Madlibs" for quests and goals, providing dynamic text
 * generation based on quest and goal templates and player progress.
 * 
 * USAGE EXAMPLE:
 * var questMadlib = QuestMadlibs.GetMadLibForQuest(questTemplate);
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 09/17/2025
 */

using Imcodec.ObjectProperty.TypeCache;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Models.Player;
using System.Linq;

namespace Imlight.CoreLib.Game.Madlibs;

internal static class QuestMadlibs {

    internal static MadlibBlock GetMadLibForQuest(QuestTemplate quest)
        => new() {
            m_madlibs = [
        new MadlibArgT_ByteString {
                        m_madlibToken = "NAME",
                        m_madlibArgument = quest.m_questTitle
                    },
                    new MadlibArgT_ByteString {
                        m_madlibToken = "LEVEL",
                        m_madlibArgument = quest.m_questLevel.ToString()
                    },
        ],
            m_blockToken = "QUEST"
        };

    internal static MadlibBlock GetAppropriateMadlibBlockForGoal(GoalTemplate gTemplate, GoalInstance gInstance)
        => gTemplate.m_goalType switch {
            GOAL_TYPE.GOAL_TYPE_WAYPOINT => GetMadlibBlockForWaypointGoal(gTemplate),
            GOAL_TYPE.GOAL_TYPE_PERSONA => GetMadlibBlockForPersonaGoal(gTemplate),
            GOAL_TYPE.GOAL_TYPE_BOUNTY or GOAL_TYPE.GOAL_TYPE_BOUNTYCOLLECT => GetMadlibBlockForBountyGoal(gTemplate, gInstance),
            _ => new MadlibBlock()
        };

    private static MadlibBlock GetMadlibBlockForWaypointGoal(GoalTemplate gTemplate)
        => new() {
            m_madlibs = [
                new MadlibArgT_ByteString {
                    m_madlibToken = "NAME",
                    m_madlibArgument = gTemplate.m_goalTitle
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "LOCATION",
                    m_madlibArgument = gTemplate.m_locationName
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "TALLYTEXT",
                    m_madlibArgument = gTemplate.m_tallyCounter?.m_descriptor ?? string.Empty
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "TALLYTEXT2",
                    m_madlibArgument = gTemplate.m_tallyCounter?.m_descriptor2 ?? string.Empty
                },
            ],
            m_blockToken = "GOAL"
        };

    private static MadlibBlock GetMadlibBlockForPersonaGoal(GoalTemplate gTemplate) {
        // Grab the completion dialog for this goal. It will determine the dialog that
        // players see when they complete the goal. This dialog should always be from the NPC.
        if (gTemplate.m_dialogList is not ActorDialogList dialogList) {
            throw new System.Exception("Persona goals must have an ActorDialogList assigned to m_dialogList.");
        }

        var completionDialogEntry = dialogList.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Completion") 
            ?? throw new System.Exception("Persona goals must have a Completion dialog entry in their ActorDialogList.");
        var templateId = (completionDialogEntry?.m_dialogEntries?.FirstOrDefault()?.m_actorTemplateID)
            ?? throw new System.Exception("Persona goals must have an ActorTemplateID assigned in their Completion dialog entry.");

        // Grab the actual template from the template ID.
        var npcTemplate = CoreObjectFactory.GetCoreTemplate(templateId);
        if (npcTemplate is not GameObjectTemplate npcGameObjectTemplate) {
            throw new System.Exception("Persona goals must have a valid GameObjectTemplate assigned to their Completion dialog entry.");
        }

        // Resolve the fullname display locale ID into a FIRSTNAME/LASTNAME format.
        var npcFullname = npcGameObjectTemplate.m_displayName;
        (string firstName, string lastName) = Locale.GetEnglishNameKeys(npcFullname);

        var madLibs = new MadlibBlock() {
            m_madlibs = [
                new MadlibArgT_ByteString {
                    m_madlibToken = "NAME",
                    m_madlibArgument = gTemplate.m_goalTitle
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "LOCATION",
                    m_madlibArgument = gTemplate.m_locationName
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "FIRSTNAME",
                    m_madlibArgument = firstName
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "LASTNAME",
                    m_madlibArgument = lastName
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "TITLE",
                    m_madlibArgument = "",
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "FULLNAME",
                    m_madlibArgument = "NPCFormats_Goal_First_Last"
                }
            ],
            m_blockToken = "GOAL"
        };

        return madLibs;
    }

    private static MadlibBlock GetMadlibBlockForBountyGoal(GoalTemplate gTemplate, GoalInstance gInstance) {
        if (gTemplate is not BountyGoalTemplate bountyGoal) {
            return new MadlibBlock();
        }

        return new MadlibBlock {
            m_madlibs = [
                new MadlibArgT_ByteString {
                    m_madlibToken = "NAME",
                    m_madlibArgument = gTemplate.m_goalTitle
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "LOCATION",
                    m_madlibArgument = gTemplate.m_locationName
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "TALLYTEXT",
                    m_madlibArgument = bountyGoal.m_tallyCounter?.m_descriptor ?? string.Empty
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "TALLYTEXT2",
                    m_madlibArgument = bountyGoal.m_tallyCounter?.m_descriptor2 ?? string.Empty
                },
                new MadlibArgT_int {
                    m_madlibToken = "COUNT",
                    m_madlibArgument = gInstance?.CurrentProgress ?? 0
                },
                new MadlibArgT_int {
                    m_madlibToken = "TOTAL",
                    m_madlibArgument = bountyGoal.m_tallyCounter?.m_count ?? 0
                },
                new MadlibArgT_int {
                    m_madlibToken = "SUBSCRIBER_TOTAL",
                    m_madlibArgument = bountyGoal.m_tallyCounter?.m_count ?? 0
                }
            ],
            m_blockToken = "GOAL"
        };
    }
    
}