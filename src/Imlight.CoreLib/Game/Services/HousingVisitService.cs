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
 * HOUSING VISIT SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Handles the messages of players who share a house: the owner's send away, the visitor's fallback request, and
 * the setting that hides a player from friends.
 *
 * USAGE EXAMPLE:
 * Internal service handling housing visit messages within the game server session.
 *
 * NOTE:
 * - Sending a player away does not move them. The visitor's client answers MSG_REQUESTSENDAWAY with MSG_GOHOME, and
 *   ZoneService takes it from there.
 * - A visitor's client sends MSG_REQUESTFALLBACK by itself whenever a house zone loads. The server answers by checking
 *   that the visitor is still welcome and sending them away if they are not.
 * - MSG_HOUSINGZONEPLAYER and MSG_HOUSINGZONEPLAYERLIST are read only by the client's castle magic, so they belong to
 *   that feature.
 *
 * TODO:
 * - What does the live server do with MSG_REQUESTFALLBACK? Compare with a capture of a friend visiting.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/15/2026
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Game.Housing.Access;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;

namespace Imlight.CoreLib.Game.Services;

internal class HousingVisitService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const int QUERY_VISITOR_TIMEOUT_SECONDS = 5;
    private const byte SEND_AWAY_ACCEPTED = 0;
    private const byte SEND_AWAY_REFUSED_FOR_STAFF = 1;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new HousingVisitService(parentActor));

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_SETINVISIBLETOFRIENDS))]
    private void ReceiveSetInvisibleToFriends(WIZARDHOUSING_50_PROTOCOL.MSG_SETINVISIBLETOFRIENDS message) {
        var wizard = GetActiveWizard();
        wizard.SetInvisibleToFriends(message.InvisibleToFriends != 0);

        Logger.Debug("Character {CharId} is {State} to friends.",
            Logger.Args(wizard.CharId, message.InvisibleToFriends != 0 ? "invisible" : "visible"));
    }

    [MessageHandler(typeof(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTSENDAWAY))]
    private void ReceiveRequestSendAway(WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTSENDAWAY message) {
        var wizard = GetActiveWizard();
        var house = wizard.AttachedLotGid == 0 ? null : HouseCollection.Load(wizard.AttachedLotGid);
        if (house is null || house.OwnerCharId != wizard.CharId) {
            HandleForgedRequest("send a player away from a house that is not theirs");

            return;
        }

        if (!TryFindVisitor(wizard, message.TargetPlayer, out var visitor, out var visitorActor)) {
            Logger.Debug("Character {CharId} asked to send away {TargetPlayer}, who is not in the house.",
                Logger.Args(wizard.CharId, message.TargetPlayer));

            return;
        }

        // The client keeps its own staff from being sent away.
        if (visitor.Account?.AuthLevel > AuthLevel.None) {
            SendToSocket(new WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTSENDAWAY {
                TargetPlayer = visitor.GameObjectID,
                ISCSR = SEND_AWAY_REFUSED_FOR_STAFF
            });

            return;
        }

        visitorActor.Tell(CreateSendAway(visitor.GameObjectID), Self);
        Logger.Debug("Character {CharId} sent character {VisitorId} away from house {LotGid}.",
            Logger.Args(wizard.CharId, visitor.CharId, house.LotGid));
    }

    [MessageHandler(typeof(WIZARD3_56_PROTOCOL.MSG_REQUESTFALLBACK))]
    private void ReceiveRequestFallback(WIZARD3_56_PROTOCOL.MSG_REQUESTFALLBACK message) {
        var wizard = GetActiveWizard();
        if (wizard.AttachedLotGid == 0) {
            return;
        }

        var house = HouseCollection.Load(wizard.AttachedLotGid);
        if (HouseAccess.Evaluate(wizard, house) != HouseAccessLevel.Denied) {
            return;
        }

        Logger.Debug("Character {CharId} is not welcome in house {LotGid} any more and is sent away.",
            Logger.Args(wizard.CharId, wizard.AttachedLotGid));
        SessionActor.ActorRef.Tell(CreateSendAway(wizard.GameObjectID), Self);
    }

    private bool TryFindVisitor(Wizard owner, ulong targetPlayer, out Wizard visitor, out ActorSelection visitorActor) {
        visitor = null;
        visitorActor = null;

        // The decorating window names a player by object id and the friends list by character id.
        var candidates = new List<ulong> { targetPlayer };
        if (Wizard.TryGetCharacterId(targetPlayer, out var fromObjectId)) {
            candidates.Add(fromObjectId);
        }

        foreach (var charId in candidates.Where(candidate => candidate != 0 && candidate != owner.CharId).Distinct()) {
            if (!TryGetOnlinePlayer(charId, out var online)) {
                continue;
            }

            var selection = Context.ActorSelection(online.ActorPath);
            var response = selection
                .Ask<CHARACTER_103_PROTOCOL.MSG_CHARACTER>(
                    new CHARACTER_103_PROTOCOL.MSG_QUERYACTIVEWIZARD(),
                    TimeSpan.FromSeconds(QUERY_VISITOR_TIMEOUT_SECONDS))
                .Result;
            if (response?.Wizard is { } candidate && candidate.AttachedLotGid == owner.AttachedLotGid) {
                visitor = candidate;
                visitorActor = selection;

                return true;
            }
        }

        return false;
    }

    private static WIZARDHOUSING_50_PROTOCOL.MSG_REQUESTSENDAWAY CreateSendAway(ulong targetPlayer)
        => new() {
            TargetPlayer = targetPlayer,
            ISCSR = SEND_AWAY_ACCEPTED
        };

}
