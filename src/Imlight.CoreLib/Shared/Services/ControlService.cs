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
 */

using System;
using System.Diagnostics;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imlight.Common;
using Imlight.CoreLib.Shared.Networking;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Services;

internal class ControlService : MessageService, IWithTimers {

    public ITimerScheduler Timers { get; set; }

    private readonly byte _keepAliveInterval = ConfigurationManager.Settings["Advanced.KeepAliveInterval"].AsByte();
    private readonly byte _keepAliveRspWaitTime = ConfigurationManager.Settings["Advanced.KeepAliveRspWaitTime"].AsByte();

    private bool _sessionValid;
    private readonly Stopwatch _responseStopwatch;
    private bool _isWaitingForHeartbeatResponse;
    private bool _isInGameServer;

    public ControlService(SessionActor parentActor) : base(parentActor) {
        this._responseStopwatch = new Stopwatch();

        SendSessionOffer();
    }

    protected static Props Props(SessionActor parentActor) 
        => Akka.Actor.Props.Create(() => new ControlService(parentActor));

    protected override void ConfigureReceivers() {
        // These are sent from self on interval to remind the actor of the session heartbeat.
        Receive<string>(s => s == "KeepAliveHeartbeat", x => SendHeartbeat());
        Receive<string>(s => s == "KeepAliveEndTimes", x => ReceiveKeepAliveEndTimes());
        Receive<string>(s => s == "SessionAcceptTimer", s => SessionAcceptTimer());

        base.ConfigureReceivers();
    }

    private void SendSessionOffer() {
        // Ask the game client for a session.
        var currentUnixTimestamp = (uint) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        var timestampUpper = (int) (currentUnixTimestamp >> 32);
        var timestampLower = (int) (currentUnixTimestamp & uint.MaxValue);
        var millisecondsIntoCurrentSecond = (uint) (DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);

        var offer = new ControlMessageProtocol.SessionOffer() {
            SessionId = SessionActor.SessionID,
            TimestampUpper = timestampUpper,
            TimestampLower = timestampLower,
            Milliseconds = millisecondsIntoCurrentSecond,
        };

        // Set SessionActor variables.
        SessionActor.OfferTime = currentUnixTimestamp;
        SessionActor.OfferMillisecondsIntoSecond = millisecondsIntoCurrentSecond;

        SendToSocket(offer);

        // Start the stopwatch so we can later get RTT (ping).
        _responseStopwatch.Restart();

        // Send a message to ourselves to check if we've received a response.
        var timer = TimeSpan.FromSeconds(_keepAliveRspWaitTime);
        Timers.StartSingleTimer("SessionAcceptTimer", "SessionAcceptTimer", timer);
    }

    [MessageHandler(typeof(ControlMessageProtocol.SessionAccept))]
    private void ReceiveSessionAccept(ControlMessageProtocol.SessionAccept message) {
        // The game client approves of the agreed upon session.
        _responseStopwatch.Stop();
        if (message.SessionId != SessionActor.SessionID) {
            throw new Exception($"SessionActor [{SessionActor.SessionID}] misaligned Session ID.");
        }

        // Set local variables.
        _sessionValid = true;
        _isWaitingForHeartbeatResponse = false;

        // The session is now valid. For optimization purposes, our parent SessionActor doesn't load
        // all the services on creation. Instead, we wait for the session to be valid.
        // We need to now tell our SessionActor that the session is created, and to grab the rest of its services.
        var msg = new SERVICE_101_PROTOCOL.MSG_GETALLSERVICES();
        SessionActor.ActorRef.Tell(msg);
        SessionActor
            .ActorRef
            .Tell(new SERVER_100_PROTOCOL.MSG_PING() { Ping = _responseStopwatch.ElapsedMilliseconds });

        // Once the session is created, we need to send a heartbeat to keep it active.
        // To do that. we'll have this actor send a message to itself on interval to check on the heartbeat.
        var heartbeatInterval = TimeSpan.FromSeconds(_keepAliveInterval);
        Timers.StartPeriodicTimer("SessionAcceptTimer", "SessionAcceptTimer", heartbeatInterval, heartbeatInterval);

        _responseStopwatch.Reset();
    }

    [MessageHandler(typeof(ControlMessageProtocol.KeepAlive))]
    private void ReceiveKeepAlive(ControlMessageProtocol.KeepAlive message) {
        if (message.SessionId != SessionActor.SessionID) {
            throw new Exception($"SessionActor [{SessionActor.SessionID}] misaligned Session ID.");
        }

        var millisecondsIntoCurrentSecond = (ushort) (DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1000);
        var rsp = new ControlMessageProtocol.KeepAliveResponse() {
            SessionId = SessionActor.SessionID,
            Milliseconds = millisecondsIntoCurrentSecond,
            ElapsedSessionTime = message.ElapsedSessionTime,
        };

        SendToSocket(rsp);
    }

    [MessageHandler(typeof(ControlMessageProtocol.KeepAliveResponse))]
    private void ReceiveKeepAliveRsp(ControlMessageProtocol.KeepAliveResponse message) {
        _responseStopwatch.Reset();
        _isWaitingForHeartbeatResponse = false;

        SessionActor
            .ActorRef
            .Tell(new SERVER_100_PROTOCOL.MSG_PING() { Ping = _responseStopwatch.ElapsedMilliseconds });
    }

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT))]
    private void ReceiveHalt(SERVICE_101_PROTOCOL.MSG_OPCODE_HALT message) => _isInGameServer = true;

    [MessageHandler(typeof(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME))]
    private void ReceiveResume(SERVICE_101_PROTOCOL.MSG_OPCODE_RESUME message) => _isInGameServer = false;

    private void SendHeartbeat() {
        if (!_sessionValid) {
            Logger.Error("{Name} {SessionID} tried to send heartbeat to an invalid session",
                Logger.Args(nameof(SessionActor), SessionActor.SessionID));
            CloseSession();
            return;
        }

        // We're going to send a heartbeat to our connected session.
        // If we don't receive a response for `KEEP_ALIVE_RSP_WAIT_TIME` time, we'll drop the session.
        _isWaitingForHeartbeatResponse = true;

        var keepAlive = new ControlMessageProtocol.KeepAliveServer() {
            SessionId = SessionActor.SessionID,
            Milliseconds = (uint) 0,
        };

        SendToSocket(keepAlive);

        // Send message to self after x seconds to remind CommunicationActor to check
        // the status of the KeepAlive.
        var reminderTime = TimeSpan.FromSeconds(_keepAliveRspWaitTime);
        Timers.StartSingleTimer("KeepAliveEndTimes", "KeepAliveEndTimes", reminderTime);

        _responseStopwatch.Start();
    }

    private void ReceiveKeepAliveEndTimes() {
        if (!_isWaitingForHeartbeatResponse || _isInGameServer) {
            return;
        }

        CloseSession();
    }

    private void SessionAcceptTimer() {
        if (_sessionValid) {
            return;
        }

        Logger.Verbose("SessionActor {SessionID} " +
                  "did not return a SessionAccept message in time", Logger.Args(SessionActor.SessionID));
        CloseSession();
    }

}
