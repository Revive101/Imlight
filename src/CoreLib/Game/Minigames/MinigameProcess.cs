/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.MessageLayer;
using Imlight.Common.ObjectProperty;
using Imlight.Common.ObjectProperty.PropertyReflection;
using Imlight.CoreLib.Game.Minigames;
using Imlight.CoreLib.Game.Processes;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Misc;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using static Imlight.Common.Caches.TypeCache;

internal sealed class MinigameProcess : Process {

    private const uint QUERY_WIZARD_TIMEOUT_IN_MS = 5000;

    private bool IsMinigameActive { get; set; }

    // todo: disallow this below. The rewards should come from the actual drop tables.
    private static readonly RewardTier[] s_manaTiers = [
        new RewardTier(0, 0),          // No reward for first threshold
        new RewardTier(0.25f, 0.5f),   // Bronze tier
        new RewardTier(0.5f, 0.75f),   // Silver tier
        new RewardTier(1.0f, 1.0f)     // Gold tier
    ];

    private static readonly RewardTier[] s_goldTiers = [
        new RewardTier(0, 0),    // No gold before gold tier
        new RewardTier(5, 10),   // Bronze gold tier
        new RewardTier(10, 15),  // Silver gold tier
        new RewardTier(15, 30)   // Gold gold tier
    ];

    private record RewardTier(float MinPercent, float MaxPercent);

    private readonly byte _minigameIndex;
    private readonly MinigameInfo _minigameInfo;
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
        .OnBehaviors(SerializerOptions.Behaviors.None)
        .OnPropertyMask((SerializerOptions.PropertyFlags) 5);
    private readonly byte[] _allowedProtocolIds = [25, 40, 41, 42, 43, 44, 45, 46, 47, 54];

    // ctor
    public MinigameProcess(string processName, uint processId, byte minigameIndex) : base(processName, processId) {
        this._minigameIndex = minigameIndex;
        this._minigameInfo = Imlight.CoreLib.Game.Minigames.MinigameConfig.GetMinigameInfo(minigameIndex);

        if (this._minigameInfo == null) {
            throw new Exception($"Minigame at index {minigameIndex} not found.");
        }
    }

    [MessageHandler(typeof(IMessage))]
    internal void ReceiveElse(IMessage message) {
        HadActivity = true;

        if (!_allowedProtocolIds.Contains(message.ServiceId)) {
            return;
        }

        // Ignore any message ID of 2. This is a movement message that's not used by the client.
        if (message.MessageOrder == 1) {
            // This is a connect message.
            HandleConnect();
        }
        else if (message.MessageOrder == 3) {
            // This is a rewards message. 
            var score = GetScoreFromGenericIMessage(message);
            HandleRewards(score);
        }
    }

    private void HandleConnect() {
        IsMinigameActive = true;

        var msg = new WIZARD_12_PROTOCOL.MSG_ENTERMINIGAME();
        Sender.Tell(msg, Self);
    }

    private void HandleRewards(int score) {
        var leaderboard = CreateScoreTrackingList();
        var leaderboardData = _serializer.Serialize(leaderboard);

        // Handle leaderboard query before game completion.
        if (score == -1) {
            SendLeaderboardResponse(leaderboardData);

            return;
        }

        var wizard = QuerySenderWizard();
        if (wizard == null) {
            Logger.Error("{0} {1} failed to query character data.",
                Logger.Args(nameof(MinigameProcess), ProcessName));

            return;
        }

        // Generate and send final rewards response.
        var loot = GetLootInfo(score, wizard);
        SendFinalRewardsResponse(score, leaderboard, loot, wizard);
    }

    private void SendLeaderboardResponse(byte[] leaderboardData) {
        var reply = new WIZARD_12_PROTOCOL.MSG_MINIGAMEREWARDS {
            GlobalID = 0,
            Data = "",
            Scores = leaderboardData,
            MinigameIndex = _minigameIndex,
            FinalPhase = 0,
            Score = -1,
            Success = 0
        };
        Sender.Tell(reply, Self);
    }

    private void SendFinalRewardsResponse(int score, ScoreTrackingList leaderboard, LootInfoList loot, Wizard wizard) {
        // Track the score for the player.
        var genderString = wizard.PlayerNameBehavior.Gender
            .ToString()
            .Replace("eGender.", "");
        var scoreTrack = new Imlight.CoreLib.WizardData.Models.Misc.ScoreTracking {
            MinigameName = _minigameInfo.m_name,
            GameScore = score,
            GamerId = (GID) wizard.CharId,
            GamerNameIndices = wizard.PlayerNameBehavior.NameIndices,
            GamerNameOverride = wizard.PlayerNameBehavior.NameOverride,
            GamerGender = genderString
        };
        ScoreTrackingCollection.AddScoreTracking(scoreTrack);

        // Check if the user's score has beaten any of the top scores.
        for (var i = 0; i < leaderboard.m_scores.Count; i++) {
            if (score > leaderboard.m_scores[i].m_gameScore) {
                leaderboard.m_scores[i] = scoreTrack.GetClientBehaviorInstance();
                break;
            }
        }
        var leaderboardData = _serializer.Serialize(leaderboard);

        // Success is only true if the user has gotten any of the rewards.
        var success = loot.m_loot.Count > 0 || loot.m_goldInfo.m_goldAmount > 0 ? 1 : 0;

        var replyEnd = new WIZARD_12_PROTOCOL.MSG_MINIGAMEREWARDS {
            GlobalID = 0,
            Data = _serializer.Serialize(loot),
            Scores = leaderboardData,
            MinigameIndex = _minigameIndex,
            FinalPhase = 1,
            Score = score,
            Success = success
        };
        Sender.Tell(replyEnd, Self);
    }

    private LootInfoList GetLootInfo(int score, Wizard wizard) {
        // Find the score threshold that the player has reached.
        var scoreThresholds = _minigameInfo.m_scoreThresholds;
        var thresholdIndex = FindThresholdIndex(score, scoreThresholds);
        var lootInfo = new LootInfoList() {
            m_goldInfo = new GoldLootInfo(),
            m_loot = [],
            m_lootRarityList = new(),
        };

        AddManaReward(score, thresholdIndex, wizard, lootInfo);
        AddGoldReward(thresholdIndex, lootInfo);

        return lootInfo;
    }

    private Wizard QuerySenderWizard() {
        var queryMsg = new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD();
        try {
            var timeout = TimeSpan.FromMilliseconds(QUERY_WIZARD_TIMEOUT_IN_MS);
            var response = Sender
                .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(queryMsg, timeout)
                .Result;

            return response.Wizard;
        }
        catch {
            Logger.Error("{0} {1} failed to query character data in {2} ms.",
                Logger.Args(nameof(MinigameProcess), ProcessName, QUERY_WIZARD_TIMEOUT_IN_MS));

            return null;
        }
    }

    private ScoreTrackingList CreateScoreTrackingList() {
        var scoreTrackingList = new ScoreTrackingList {
            m_scores = []
        };

        var leaderboard = ScoreTrackingCollection.GetLeaderboard(ProcessName);
        foreach (var entry in leaderboard) {
            scoreTrackingList.m_scores.Add(entry.GetClientBehaviorInstance());
        }

        return scoreTrackingList;
    }

    private int GetScoreFromGenericIMessage(IMessage message) {
        try {
            var scoreField = message.GetType().GetField("score",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            return scoreField != null
                ? (int) scoreField.GetValue(message)
                : -1;
        }
        catch (Exception ex) {
            Logger.Error("{0} {1} failed to get score: {2}",
                Logger.Args(nameof(MinigameProcess), ProcessName, ex.Message));
            return -1;
        }
    }

    private static int FindThresholdIndex(int score, List<int> scoreThresholds) {
        for (var i = 0; i < scoreThresholds.Count; i++) {
            if (score < scoreThresholds[i]) {
                return i;
            }
        }
        return scoreThresholds.Count - 1;
    }

    private static int CalculateReward(int score, int thresholdIndex, RewardTier[] tiers) {
        if (thresholdIndex < 1 || thresholdIndex >= tiers.Length) {
            return 0;
        }

        var tier = tiers[thresholdIndex];

        // For mana, calculate based on score and percentage
        if (tiers == s_manaTiers) {
            float rewardPercent = tier.MinPercent == tier.MaxPercent
                ? tier.MinPercent
                : Random.Shared.NextSingle() * (tier.MaxPercent - tier.MinPercent) + tier.MinPercent;
            return (int) (score * rewardPercent);
        }

        // For gold, return random amount in range
        return Random.Shared.Next((int) tier.MinPercent, (int) tier.MaxPercent + 1);
    }

    private static void AddManaReward(int score, int thresholdIndex, Wizard wizard, LootInfoList lootInfo) {
        var manaReward = CalculateReward(score, thresholdIndex, s_manaTiers);
        if (manaReward <= 0) {
            return;
        }

        var wizardMana = wizard.GameStats.m_baseMana;
        var manaAmount = Math.Clamp(wizardMana * manaReward, 0, wizardMana);

        lootInfo.m_loot.Add(new ManaLootInfo {
            m_lootType = LootInfo.LOOT_TYPE.LOOT_TYPE_MANA,
            m_manaAmount = manaAmount
        });
    }

    private static void AddGoldReward(int thresholdIndex, LootInfoList lootInfo) {
        var goldReward = CalculateReward(0, thresholdIndex, s_goldTiers);
        if (goldReward <= 0) {
            return;
        }

        lootInfo.m_goldInfo = new GoldLootInfo {
            m_lootType = LootInfo.LOOT_TYPE.LOOT_TYPE_GOLD,
            m_goldAmount = goldReward
        };
    }

}