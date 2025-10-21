/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imlight.Common;
using Imlight.CoreLib.Game.DropTables;
using Imlight.CoreLib.Game.Madlibs;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Commands.Protocols;

internal class CommandQuest : CommandProtocol {

    internal override string Group { get; set; } = "quest";

    [Command("offer")]
    [AuthRequired(AuthLevel.QualityAssurance)]
    private void QuestOfferCommand(string questName) {
        var wizard = Context.Character;
        
        // Check if the quest exists.
        var quest = QuestTemplateCollection.GetQuestByName(questName);
        if (quest == null) {
            InformSenderClient($"Quest '{questName}' does not exist.");

            return;
        }

        // Check if player already has this quest.
        if (wizard.HasQuest(questName)) {
            InformSenderClient($"You already have the quest '{questName}'.");

            return;
        }

        ShowQuestInfoDialog(quest);
        SendQuestOfferDialog(quest);
        SendQuestOfferCacheOption(quest);
    }

    private void ShowQuestInfoDialog(QuestTemplate quest) {
        var dialogList = quest.m_dialogList as ActorDialogList;
        var prepDialogList = dialogList?.m_dialogs.FirstOrDefault(de => de.m_dialogTag == "Prep");

        if (prepDialogList == null) {
            return;
        }

        SendActorDialog(prepDialogList, "QuestInfo");
    }

    private void SendQuestOfferDialog(QuestTemplate quest) {
        var startingGoals = quest.m_goals
            .Where(g => quest.m_startGoals.Contains(g.m_goalName))
            .ToList();

        var startingGoalCompilation = new GoalCompilation {
            m_goals = [.. startingGoals.Select(goal => new GoalEntryFull {
                m_personaName = "",
                m_goalType = (int) goal.m_goalType,
                m_tallyText = goal.m_goalTitle,
                m_goalLocation = goal.m_locationName,
                m_goalDestinationZone = goal.m_destinationZone,
                m_goalImage1 = goal.m_displayImage1,
                m_goalImage2 = goal.m_displayImage2,
                m_goalNameID = goal.m_goalNameID,
                m_goalMadlibs = QuestMadlibs.GetAppropriateMadlibBlockForGoal(goal, null)
            })]
        };

        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(startingGoalCompilation, 1, out var serializedGoals)) {
            Logger.Error("Failed to serialize starting goals for quest {0}.",
                Logger.Args(quest.m_questName));
            return;
        }

        var rewards = GetQuestRewardsFromTemplate(quest);
        if (!serializer.Serialize(rewards, 1, out var serializedRewards)) {
            Logger.Error("Failed to serialize rewards for quest {0}.",
                Logger.Args(quest.m_questName));
            return;
        }

        var questOfferMsg = new QUEST_MESSAGES_52_PROTOCOL.MSG_QUESTOFFER {
            MobileID = 0, // No specific NPC for command-based offers
            QuestName = quest.m_questName,
            QuestTitle = quest.m_questTitle,
            QuestInfo = "",
            Level = quest.m_questLevel,
            Rewards = serializedRewards,
            GoalData = serializedGoals,
            Mainline = (byte) (quest.m_mainline ? 1 : 0),
        };

        Context.SessionActor.Tell(questOfferMsg);
    }

    private void SendQuestOfferCacheOption(QuestTemplate quest) {
        var cacheMsg = new CHARACTER_103_PROTOCOL.MSG_SENDQUESTOFFERCACHEOPTION {
            Quest = quest,
        };

        Context.SessionActor.Tell(cacheMsg);
    }

    private void SendActorDialog(ActorDialog dialogEntry, string completionType, ulong questId = 0, ulong goalId = 0) {
        var serializer = new ObjectSerializer(Versionable: false);
        if (!serializer.Serialize(dialogEntry, 16, out var serializedData)) {
            Logger.Error("Failed to serialize '{0}' dialog.", Logger.Args(completionType));
            return;
        }

        var dialogMsg = new WIZARD_12_PROTOCOL.MSG_ACTORDIALOG {
            MobileID = 0, // No specific NPC for command-based dialogs
            QuestID = questId,
            GoalID = goalId,
            CompletionType = completionType,
            ActorDialog = serializedData,
            Persona = "",
            PersonaName = "",
            PersonaIcon = "",
        };

        Context.SessionActor.Tell(dialogMsg);
    }

    private LootInfoList GetQuestRewardsFromTemplate(QuestTemplate qTemplate) {
        // Quest rewards are listed in drop tables by "ResDropTable" in the completion results.
        if (qTemplate?.m_endResults is null || qTemplate.m_endResults.m_results is null) {
            return new LootInfoList();
        }

        var dropTableResults = qTemplate.m_endResults
            .m_results
            .Where(x => x is ResDropTable);

        var dropTableNames = dropTableResults
            .Select(x => (x as ResDropTable).m_tableName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        // "Roll" the drop tables to get the actual items.
        var rollResult = DropTableRoller.Roll(dropTableNames, Context.SessionActor, Context.CharacterObject, Context.Character);

        // Convert the result into something we can send over the network.
        var convertedResults = DropTableConverter.ToLootInfoList(rollResult);

        return convertedResults;
    }

}
