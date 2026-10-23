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
 * HATCHING SERVICE
 * ========================================================================
 *
 * PURPOSE:
 * Runs a player's visit to the hatching room: opens the client's hatching window, lists the partner's
 * pets, passes picks, Hatch or Lend and ready boxes between the two players, lays the egg or counts
 * the lend, and sends the player back to the sigil.
 *
 * USAGE EXAMPLE:
 * A minigame sigil sends the player to its zone (MSG_SIGILROOMENTERING); the zone's pet game room
 * says which game it runs (MSG_PETGAMEROOMJOINED), and the window opens once the client attached.
 *
 * NOTE:
 * Alone, or when the partner never arrives, both sides of the window list the player's own pets.
 * Each player's service decides its own outcome from the choices both players made.
 *
 * TODO:
 * - Every pet game behind a minigame sigil is taken for the hatching game.
 * - Does the client want MSG_ENTERMINIGAME and MSG_LEAVEMINIGAME around the room?
 * - The lender gets no reward; the lend loot tables are not granted.
 * - How long should a player wait for a partner who has not reached the room? Ten seconds is a guess.
 *
 * Created by: Jay
 * Version: KALI 1.0
 * Last Updated: 10/23/2026
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Akka.Actor;
using Imcodec.CoreObject;
using Imcodec.IO;
using Imcodec.MessageLayer.Generated;
using Imcodec.ObjectProperty;
using Imcodec.ObjectProperty.TypeCache;
using Imcodec.Types;
using Imlight.Common;
using Imlight.CoreLib.Game.Pet.Config;
using Imlight.CoreLib.Game.Pet.Games;
using Imlight.CoreLib.Game.Pet.Hatching;
using Imlight.CoreLib.Game.Pet.Progression;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;
using Imlight.CoreLib.Shared.Resources;
using Imlight.CoreLib.WizardData.Collections;
using PetGameConfig = Imlight.CoreLib.Game.Pet.Config.PetGameConfig;

namespace Imlight.CoreLib.Game.Services;

internal class HatchingService(SessionActor sessionActor) : MessageService(sessionActor) {

    private const string ROOM_TIMER_KEY = "hatch-room";
    private const string RETURN_TIMER_KEY = "hatch-return";
    private const string PARTNER_WAIT_TIMER_KEY = "hatch-partner-wait";

    private const string PICK_COMMAND_PREFIX = "set:";
    private const string READY_COMMAND_PREFIX = "ready:";
    private const string PICK_SIDE_FIELD = "side";
    private const string PICK_ID_FIELD = "id";
    private const string READY_FIELD = "confirmed";
    private const string READY_TICKED_VALUE = "1";
    private const string HATCH_CHOICE_COMMAND = "Hatch:Hatch";
    private const string LEND_CHOICE_COMMAND = "Lend:Lend";

    private const string WAITING_FOR_PARTNER_OUTCOME = "Hatch:Wait";
    private const string BOTH_HATCH_OUTCOME = "Hatch:Hatch";
    private const string HATCH_WHILE_PARTNER_LENDS_OUTCOME = "Hatch:Lend";
    private const string LEND_WHILE_PARTNER_HATCHES_OUTCOME = "Lend:Hatch";
    private const string BOTH_LEND_OUTCOME = "Lend:DoubleLend";

    private const PropertyFlags GAME_INFO_PROPERTY_MASK = PropertyFlags.Prop_Save | PropertyFlags.Prop_Copy | PropertyFlags.Prop_Public;
    private const PropertyFlags PARTNER_PROPERTY_MASK = PropertyFlags.Prop_Transmit | PropertyFlags.Prop_AuthorityTransmit;

    private static readonly TimeSpan s_partnerWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_lendReturnDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_partnerLeftReturnDelay = TimeSpan.FromSeconds(2);

    private HatchingRoom _room;

    protected static Props Props(SessionActor parentActor)
        => Akka.Actor.Props.Create(() => new HatchingService(parentActor));

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_SIGILROOMENTERING))]
    private void ReceiveSigilRoomEntering(ZONE_102_PROTOCOL.MSG_SIGILROOMENTERING message) {
        CancelRoomTimers();
        _room = new HatchingRoom(message.DestinationZone, message.PartnerCharId, message.ReturnZone, message.ReturnLocation);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PETGAMEROOMJOINED))]
    private void ReceivePetGameRoomJoined(ZONE_102_PROTOCOL.MSG_PETGAMEROOMJOINED message) {
        if (string.IsNullOrEmpty(message.GameName)) {
            return;
        }

        // A pet game's zone is PetGamesService's to run.
        if (_room is null && PetGameVisits.Contains(GetActiveWizard().CharId)) {
            return;
        }

        if (_room is null) {
            Teleport(GetActiveWizard().PreviousZone, doTeleportEffects: false);

            return;
        }

        _room.GameName = message.GameName;
        if (_room.IsDuo && message.OtherPlayers?.TryGetValue(_room.PartnerCharId, out var partnerSession) == true) {
            MeetPartner(partnerSession);
        }

        TryOpenWindow();
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE))]
    private void ReceivePostAttach(SERVICE_101_PROTOCOL.MSG_ATTACHCOMPLETE message) {
        if (_room is null) {
            return;
        }

        // Anywhere but the hatching room ends the visit, so its timers cannot pull the player back later.
        if (!string.Equals(GetActiveWizard().Zone, _room.Zone, StringComparison.OrdinalIgnoreCase)) {
            _room = null;
            CancelRoomTimers();

            return;
        }

        _room.Attached = true;
        TryOpenWindow();
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PETGAMEROOMPLAYERJOINED))]
    private void ReceivePetGameRoomPlayerJoined(ZONE_102_PROTOCOL.MSG_PETGAMEROOMPLAYERJOINED message) {
        if (_room is null || !_room.IsDuo || message.CharId != _room.PartnerCharId) {
            return;
        }

        MeetPartner(message.Session);
    }

    [MessageHandler(typeof(ZONE_102_PROTOCOL.MSG_PETGAMEROOMPLAYERLEFT))]
    private void ReceivePetGameRoomPlayerLeft(ZONE_102_PROTOCOL.MSG_PETGAMEROOMPLAYERLEFT message) {
        if (_room is null || !_room.IsDuo || message.CharId != _room.PartnerCharId || _room.Done) {
            return;
        }

        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEEND { Game = _room.GameName });
        Timers.StartSingleTimer(RETURN_TIMER_KEY, new CHARACTER_103_PROTOCOL.MSG_HATCHRETURN(), s_partnerLeftReturnDelay);
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERPETS))]
    private void ReceivePartnerPets(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERPETS message) {
        if (_room is null || !_room.IsDuo || message.CharId != _room.PartnerCharId) {
            return;
        }

        _room.PartnerPets = message.Pets ?? [];
        SendPetsToPartner();
        TrySendStart();
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERHATCHED))]
    private void ReceivePartnerHatched(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERHATCHED message) {
        if (_room is not { IsDuo: true, Lends: true, Done: false } || message.CharId != _room.PartnerCharId) {
            return;
        }

        LendPet();
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERWAITOVER))]
    private void ReceivePartnerWaitOver(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERWAITOVER message) {
        if (_room is null || _room.Started || _room.PartnerPets is not null) {
            return;
        }

        Logger.Information("{CharId}'s hatching partner did not arrive, so they hatch alone.", Logger.Args(GetActiveWizard().CharId));
        _room.HatchAlone();
        TrySendStart();
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEREADY))]
    private void ReceivePetGameReady(PET_9_PROTOCOL.MSG_PETGAMEREADY message) {
        if (_room is null || !_room.WindowOpened) {
            return;
        }

        _room.ClientReady = true;
        TrySendStart();
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEDATA))]
    private void ReceivePetGameData(PET_9_PROTOCOL.MSG_PETGAMEDATA message) {
        if (_room is not { Started: true } || (string) message.Game != _room.GameName) {
            return;
        }

        var command = ((string) message.Data).TrimEnd('\0');
        if (command.StartsWith(PICK_COMMAND_PREFIX, StringComparison.Ordinal)) {
            ReceivePick(ParseFields(command[PICK_COMMAND_PREFIX.Length..]));
        } else if (command.StartsWith(READY_COMMAND_PREFIX, StringComparison.Ordinal)) {
            ReceiveReady(ParseFields(command[READY_COMMAND_PREFIX.Length..]));
        } else if (command is HATCH_CHOICE_COMMAND or LEND_CHOICE_COMMAND) {
            ReceiveChoice(command == LEND_CHOICE_COMMAND);
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERPICK))]
    private void ReceivePartnerPick(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERPICK message) {
        if (_room is not { IsDuo: true } || message.CharId != _room.PartnerCharId) {
            return;
        }

        _room.PartnerPickId = message.PetId;
        if (_room.Started) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHSET { PetID = message.PetId });
            SendCanAfford();
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERCHOICE))]
    private void ReceivePartnerChoice(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERCHOICE message) {
        if (_room is not { IsDuo: true } || message.CharId != _room.PartnerCharId) {
            return;
        }

        _room.PartnerLends = message.Lends;
        if (_room.Started) {
            SendChoiceOutcome();
        }
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERREADY))]
    private void ReceivePartnerReady(CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERREADY message) {
        if (_room is not { IsDuo: true } || message.CharId != _room.PartnerCharId) {
            return;
        }

        _room.PartnerReady = message.Ready;
        if (_room.Started) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHREADY { Confirmed = (sbyte) (message.Ready ? 1 : 0) });
        }

        TryFinish();
    }

    [MessageHandler(typeof(PET_9_PROTOCOL.MSG_PETGAMEENDING))]
    private void ReceivePetGameEnding(PET_9_PROTOCOL.MSG_PETGAMEENDING message) {
        if (_room is null || (string) message.Game != _room.GameName) {
            return;
        }

        ReturnToSigil();
    }

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHROOMEXPIRED))]
    private void ReceiveRoomExpired(CHARACTER_103_PROTOCOL.MSG_HATCHROOMEXPIRED message)
        => ReturnToSigil();

    [MessageHandler(typeof(CHARACTER_103_PROTOCOL.MSG_HATCHRETURN))]
    private void ReceiveReturn(CHARACTER_103_PROTOCOL.MSG_HATCHRETURN message)
        => ReturnToSigil();

    private void MeetPartner(IActorRef partnerSession) {
        _room.PartnerSession = partnerSession;
        SendPetsToPartner();
    }

    private void SendPetsToPartner() {
        if (_room.PartnerSession is null || _room.PetsSentToPartner) {
            return;
        }

        var wizard = GetActiveWizard();
        _room.PartnerSession.Tell(new CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERPETS {
            CharId = wizard.CharId,
            Pets = PetHatching.GetParentCandidates(wizard),
        });
        _room.PetsSentToPartner = true;
    }

    private void TryOpenWindow() {
        if (!_room.Attached || _room.GameName is null || _room.WindowOpened) {
            return;
        }

        _room.WindowOpened = true;
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEINIT {
            Game = _room.GameName,
            Data = SerializeGameInfo(_room.GameName),
            MinLevel = HatchingRules.MinParentLevel,
            Track = GetActiveWizard().PetOwnerBehavior.GetLendsLeft(HatchingRules.LendsPerDay),
        });

        Timers.StartSingleTimer(ROOM_TIMER_KEY, new CHARACTER_103_PROTOCOL.MSG_HATCHROOMEXPIRED(), HatchingRules.RoomTime);
        if (_room.IsDuo) {
            Timers.StartSingleTimer(PARTNER_WAIT_TIMER_KEY, new CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERWAITOVER(), s_partnerWait);
        }
    }

    private void TrySendStart() {
        if (!_room.ClientReady || _room.Started || (_room.IsDuo && _room.PartnerPets is null)) {
            return;
        }

        // A partner id of zero opens the window for a solo hatch, with the player's own pets on both sides.
        var partner = new PetGameMorphPartner {
            m_characterId = (GID) _room.PartnerCharId,
            m_pets = _room.IsDuo ? [.. _room.PartnerPets] : [],
        };

        // todo: is the transmit mask what the client reads the partner's pets with?
        var serializer = new CoreObjectSerializer(versionable: false, behaviors: SerializerFlags.SerializeFlags);
        if (!serializer.Serialize(partner, PARTNER_PROPERTY_MASK, out var data)) {
            Logger.Error("Could not serialize the hatching partner of {CharId}.", Logger.Args(GetActiveWizard().CharId));

            return;
        }

        _room.Started = true;
        Timers.Cancel(PARTNER_WAIT_TIMER_KEY);
        SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMESTART { Game = _room.GameName, Data = data });
        ShowEarlyPartnerState();
    }

    private void ShowEarlyPartnerState() {
        // The partner's window may have started first; what the partner did before ours started is shown now.
        if (_room.PartnerPickId != 0) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHSET { PetID = _room.PartnerPickId });
        }

        if (_room.PartnerReady) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHREADY { Confirmed = 1 });
        }
    }

    private void ReceivePick(Dictionary<string, string> fields) {
        if (!TryGetInt(fields, PICK_SIDE_FIELD, out var side) || side is < HatchingRoom.OWN_PET_PICK_SIDE or > HatchingRoom.SOLO_SECOND_PET_PICK_SIDE) {
            return;
        }

        if (!fields.TryGetValue(PICK_ID_FIELD, out var id) || !ulong.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var petId)) {
            return;
        }

        if (PetHatching.FindParentCandidate(GetActiveWizard(), petId) is null) {
            return;
        }

        _room.Picks[side] = petId;
        if (_room.IsDuo && side == HatchingRoom.OWN_PET_PICK_SIDE) {
            _room.PartnerSession?.Tell(new CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERPICK { CharId = GetActiveWizard().CharId, PetId = petId });
        }

        SendCanAfford();
    }

    private void ReceiveReady(Dictionary<string, string> fields) {
        _room.Ready = fields.GetValueOrDefault(READY_FIELD) == READY_TICKED_VALUE;
        if (_room.IsDuo) {
            _room.PartnerSession?.Tell(new CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERREADY { CharId = GetActiveWizard().CharId, Ready = _room.Ready });
        }

        TryFinish();
    }

    private void ReceiveChoice(bool lends) {
        if (!_room.IsDuo || _room.Done) {
            return;
        }

        if (lends && GetActiveWizard().PetOwnerBehavior.GetLendsLeft(HatchingRules.LendsPerDay) == 0) {
            return;
        }

        _room.Lends = lends;
        _room.PartnerSession?.Tell(new CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERCHOICE { CharId = GetActiveWizard().CharId, Lends = lends });
        SendChoiceOutcome();
    }

    private void SendChoiceOutcome() {
        if (_room.Lends is not { } lends) {
            return;
        }

        if (_room.PartnerLends is not { } partnerLends) {
            SendGameData(WAITING_FOR_PARTNER_OUTCOME);

            return;
        }

        SendGameData((lends, partnerLends) switch {
            (false, false) => BOTH_HATCH_OUTCOME,
            (false, true) => HATCH_WHILE_PARTNER_LENDS_OUTCOME,
            (true, false) => LEND_WHILE_PARTNER_HATCHES_OUTCOME,
            (true, true) => BOTH_LEND_OUTCOME,
        });

        // Two lends make no egg, so both players choose again.
        if (lends && partnerLends) {
            _room.Lends = null;
            _room.PartnerLends = null;
        }

        SendCanAfford();
    }

    private void SendCanAfford() {
        var canAfford = _room.Lends == true || (TryGetParents(out var first, out var second) && CanPay(first, second));
        SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHCANAFFORD { CanAfford = (sbyte) (canAfford ? 1 : 0) });
    }

    private void TryFinish() {
        if (_room.Done || !_room.Ready) {
            return;
        }

        if (_room.IsDuo && (!_room.PartnerReady || !_room.ChoicesAgree)) {
            return;
        }

        if (_room.Lends == true) {
            return;
        }

        HatchEgg();
    }

    private void HatchEgg() {
        var wizard = GetActiveWizard();
        if (!TryGetParents(out var first, out var second) || !CanPay(first, second)) {
            SendToSocket(new PET_9_PROTOCOL.MSG_PETMORPHCANAFFORD { CanAfford = 0 });

            return;
        }

        if (!wizard.PetOwnerBehavior.HasFreeMorphingSlot()) {
            InformGameClient("Your last egg has not hatched yet.");

            return;
        }

        var goldCost = PetHatching.GetGoldCost(first, second);
        if (!PetHatching.TryLayEgg(SessionActor.ActorRef, wizard, first, second, out _)) {
            InformGameClient("The egg could not be made.");

            return;
        }

        _room.Done = true;
        PayGold(goldCost);
        if (_room.PartnerLends == true) {
            _room.PartnerSession?.Tell(new CHARACTER_103_PROTOCOL.MSG_HATCHPARTNERHATCHED { CharId = wizard.CharId });
        }
    }

    private void LendPet() {
        var wizard = GetActiveWizard();
        _room.Done = true;
        wizard.PetOwnerBehavior.CountLend();
        WizardCollection.UpdateCharacterPetOwnerBehavior(wizard);

        // The client never says its pop-up was closed, so the lender goes back on a timer.
        SendToSocket(new PET_9_PROTOCOL.MSG_LENTPET());
        Timers.StartSingleTimer(RETURN_TIMER_KEY, new CHARACTER_103_PROTOCOL.MSG_HATCHRETURN(), s_lendReturnDelay);
    }

    private void PayGold(int goldCost) {
        if (goldCost <= 0) {
            return;
        }

        var wizard = GetActiveWizard();
        wizard.RemoveGold(goldCost);
        SendToSocket(new WIZARD_12_PROTOCOL.MSG_UPDATEGOLD {
            Gold = wizard.GameStats.m_currentGold,
            MaxGold = wizard.GameStats.m_baseGoldPouch,
        });
    }

    private bool TryGetParents(out WizClientObjectItem first, out WizClientObjectItem second) {
        var wizard = GetActiveWizard();
        first = PetHatching.FindParentCandidate(wizard, _room.Picks[HatchingRoom.OWN_PET_PICK_SIDE]);
        second = _room.IsDuo
            ? _room.PartnerPets?.FirstOrDefault(pet => pet.m_globalID == _room.PartnerPickId)
            : PetHatching.FindParentCandidate(wizard, _room.Picks[HatchingRoom.SOLO_SECOND_PET_PICK_SIDE]);

        return first is not null && second is not null && first.m_globalID != second.m_globalID;
    }

    private bool CanPay(WizClientObjectItem first, WizClientObjectItem second)
        => GetActiveWizard().GameStats.m_currentGold >= PetHatching.GetGoldCost(first, second);

    private void SendGameData(string data)
        => SendToSocket(new PET_9_PROTOCOL.MSG_PETGAMEDATA { Game = _room.GameName, Data = data });

    private void ReturnToSigil() {
        var room = _room;
        _room = null;
        CancelRoomTimers();
        if (room is null) {
            return;
        }

        Teleport(room.ReturnZone, doTeleportEffects: false, destinationLocation: room.ReturnLocation);
    }

    private void CancelRoomTimers() {
        Timers.Cancel(ROOM_TIMER_KEY);
        Timers.Cancel(RETURN_TIMER_KEY);
        Timers.Cancel(PARTNER_WAIT_TIMER_KEY);
    }

    private static ByteString SerializeGameInfo(string gameName) {
        // todo: does the client read the game's own info from MSG_PETGAMEINIT, and with this mask?
        var info = PetGameConfig.FindGame(gameName);
        var serializer = new ObjectSerializer(Versionable: false, Behaviors: SerializerFlags.None);

        return info is not null && serializer.Serialize(info, GAME_INFO_PROPERTY_MASK, out var data) ? data : new ByteString();
    }

    private static Dictionary<string, string> ParseFields(string fields)
        => fields.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(field => field.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .DistinctBy(pair => pair[0], StringComparer.Ordinal)
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);

    private static bool TryGetInt(Dictionary<string, string> fields, string name, out int value) {
        value = 0;

        return fields.TryGetValue(name, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

}
