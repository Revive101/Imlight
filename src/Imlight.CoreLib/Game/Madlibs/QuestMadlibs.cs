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
 * Last Updated: 08/21/2026
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
            GOAL_TYPE.GOAL_TYPE_BOUNTY 
                or GOAL_TYPE.GOAL_TYPE_BOUNTYCOLLECT 
                or GOAL_TYPE.GOAL_TYPE_USAGE 
                    => GetMadlibBlockForTalliedGoal(gTemplate, gInstance),
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
        string firstName = string.Empty;
        string lastName = string.Empty;
        string title = string.Empty;
        string nickname = string.Empty;

        if (gTemplate.m_dialogList is ActorDialogList dialogList) {
            var completionDialogEntry = dialogList.m_dialogs
                .FirstOrDefault(de => de.m_dialogTag == "Completion");

            // Retail sends the Completion dialog's own NPC madlib block keys;
            // the blocks are ordered to match the entries.
            var npcBlock = completionDialogEntry?.m_madlibs?.FirstOrDefault()?.m_madlibBlock;
            if (npcBlock is not null) {
                firstName = GetStringArgument(npcBlock, "FIRSTNAME") ?? firstName;
                lastName = GetStringArgument(npcBlock, "LASTNAME") ?? lastName;
                title = GetStringArgument(npcBlock, "TITLE") ?? title;
                nickname = GetStringArgument(npcBlock, "NICKNAME") ?? nickname;
            }
            else {
                // Fallback: resolve the display name from the entry's actor template.
                var templateId = completionDialogEntry?.m_dialogEntries
                    ?.FirstOrDefault()?.m_actorTemplateID ?? 0;

                if (templateId != 0) {
                    var npcTemplate = CoreObjectFactory.GetCoreTemplate(templateId);
                    if (npcTemplate is GameObjectTemplate npcGameObjectTemplate) {
                        var npcFullname = npcGameObjectTemplate.m_displayName;
                        (firstName, lastName) = Locale.GetEnglishNameKeys(npcFullname);
                    }
                }
            }
        }

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
                    m_madlibArgument = title
                },
                new MadlibArgT_ByteString {
                    m_madlibToken = "NICKNAME",
                    m_madlibArgument = nickname
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

    private static string GetStringArgument(MadlibBlock block, string token)
        => block.m_madlibs?.OfType<MadlibArgT_ByteString>()
            .FirstOrDefault(arg => arg.m_madlibToken == token)?.m_madlibArgument;

    private static MadlibBlock GetMadlibBlockForTalliedGoal(GoalTemplate gTemplate, GoalInstance gInstance)
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
                new MadlibArgT_int {
                    m_madlibToken = "COUNT",
                    m_madlibArgument = gInstance?.CurrentProgress ?? 0
                },
                new MadlibArgT_int {
                    m_madlibToken = "TOTAL",
                    m_madlibArgument = gTemplate.m_tallyCounter?.m_count ?? 0
                },
                new MadlibArgT_int {
                    m_madlibToken = "SUBSCRIBER_TOTAL",
                    m_madlibArgument = gTemplate.m_tallyCounter?.m_count ?? 0
                }
            ],
            m_blockToken = "GOAL"
        };

}