/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using Akka.Actor;
using Imlight.Common;
using Imlight.Common.Caches;
using Imlight.Common.MessageLayer;
using Imlight.Common.ObjectProperty;
using Imlight.CoreLib.Game.Minigames;
using Imlight.CoreLib.Game.Processes;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using System;
using System.Linq;

internal sealed class MinigameProcess : Process {
    
    private const uint QUERY_WIZARD_TIMEOUT_IN_MS = 5000;

    private bool IsMinigameActive { get; set; }

    private readonly byte _minigameIndex;
    private readonly TypeCache.MinigameInfo _minigameInfo;
    private readonly ObjectSerializer _serializer = new ObjectSerializer()
        .OnBehaviors(SerializerOptions.Behaviors.None)
        .OnPropertyMask((SerializerOptions.PropertyFlags) 5);
    private readonly byte[] _allowedProtocolIds = [25, 40, 41, 42, 43, 44, 45, 46, 47, 54];

    // ctor
    public MinigameProcess(string processName, uint processId, byte minigameIndex) : base(processName, processId) {
        this._minigameIndex = minigameIndex;
        this._minigameInfo = MinigameConfig.GetMinigameInfo(minigameIndex);

        if (this._minigameInfo == null) {
            throw new Exception($"Minigame at index {minigameIndex} not found.");
        }
    }

    [MessageHandler(typeof(IMessage))]
    internal void ReceiveElse(IMessage message) {
        HadActivity = true;

        if (!_allowedProtocolIds.Contains(message.ServiceId)) {
            Logger.Warning("{0} {1} received unexpected message ID {2}.",
                Logger.Args(nameof(MinigameProcess), ProcessName, message.ServiceId));

            return;
        }

        // Ignore any message ID of 2. This is a movement message that's not used by the client.
        if (message.MessageOrder == 1) {
            // This is a connect message.
            HandleConnect();
        }
        else if (message.MessageOrder == 3) {
            // This is a rewards message.
            HandleRewards();
        }
    }

    private void HandleConnect() {
        IsMinigameActive = true;

        var msg = new WIZARD_12_PROTOCOL.MSG_ENTERMINIGAME();
        Sender.Tell(msg, Self);
    }

    private void HandleRewards() {
        IsMinigameActive = false;
        var leaderboard = CreateScoreTrackingList();
        var leaderboardData = _serializer.Serialize(leaderboard);

        // If the minigame hasn't started yet, the player is just looking for
        // leaderboard data.
        if (!IsMinigameActive) {
            var reply = new WIZARD_12_PROTOCOL.MSG_MINIGAMEREWARDS {
                GlobalID = (ushort) ProcessId,
                Data = "",
                Scores = leaderboardData,
                MinigameIndex = _minigameIndex,
                FinalPhase = 0,
                Score = -1,
                Success = 0
            };
            Sender.Tell(reply, Self);

            return;
        }

        // Otherwise, the player has completed the minigame and is looking for rewards.
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

    private TypeCache.ScoreTrackingList CreateScoreTrackingList() {
        var leaderboard = ScoreTrackingCollection.GetLeaderboard(_minigameInfo.m_name);
        var scores = new TypeCache.ScoreTrackingList();

        foreach (var leaderboardEntry in leaderboard) {
            var clientType = leaderboardEntry.GetClientBehaviorInstance();
            scores.m_scores.Add(clientType);
        }

        return scores;
    }

}