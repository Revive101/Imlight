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
 * SOCKET SENDER
 * ========================================================================
 * 
 * PURPOSE:
 * Manages socket-level packet sending for network sessions, 
 * handling message encoding and transmission with error handling.
 * 
 * USAGE EXAMPLE:
 * // Socket sender is typically created by SessionActor
 * // Handles outgoing network packet transmission
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 3/18/2025
 */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;

namespace Imlight.CoreLib.Shared.Networking;

internal sealed class SocketSender : ReceiveActor, IDisposable {
    
    private readonly IActorRef _sessionActorRef;
    private readonly Socket _socket;
    private readonly ushort _sessionid;
    private readonly List<Type> _suppressedPackets = new() {
            typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVE),
            typeof(GAME_5_PROTOCOL.MSG_CLIENTMOVESTATE),
            typeof(GAME_5_PROTOCOL.MSG_SERVERMOVE),
            typeof(GAME_5_PROTOCOL.MSG_NEWOBJECT),
            typeof(GAME_5_PROTOCOL.MSG_REMOVEOBJECT),
            typeof(GAME_5_PROTOCOL.MSG_MOVESTATE),
            typeof(LOGIN_7_PROTOCOL.MSG_LOGIN_NOT_AFK),
            typeof(ControlMessageProtocol.KeepAlive),
            typeof(ControlMessageProtocol.KeepAliveResponse)
        };
    private bool _isDisposed;
    private bool _isSending;

    // ctor
    public SocketSender(IActorRef sessionActor, Socket socket, ushort sessionid) {
        this._sessionActorRef = sessionActor;
        this._socket = socket;
        this._sessionid = sessionid;

        Receive<string>(x => x == "Close", x => Dispose());
        Receive<IMessage>(SendToSocket);
    }

    public void Dispose() {
        if (_isDisposed) {
            return;
        }

        _isDisposed = true;
        _sessionActorRef.Ask("Close");

        _socket?.Close();
        _socket?.Dispose();
    }

    private void SendToSocket(IMessage message) {
        if (!_socket.Connected) {
            Dispose();
        }
        if (_isSending) {
            Logger.Error("SessionActor {SessionId} send failure: " +
                         "Synchronous send operation already in progress.", Logger.Args(_sessionid));
                         
            return;
        }
        if (_isDisposed) {
            return;
        }

        var data = MessageEncoder.Encode(message);
        _isSending = true;

        try {
            var bytesSent = _socket.Send(data);
            if (bytesSent != data.Length) {
                throw new SessionFatalException(
                    $"SessionActor [{_sessionid}] send failure: " +
                    $"Sent {bytesSent} bytes out of {data.Length} bytes.");
            }
        }
        catch (SocketException ex) {
            throw new SessionFatalException($"SessionActor [{_sessionid}] send failure: {ex.SocketErrorCode}");
        }
        finally {
            _isSending = false;
        }

        LogSentPacket(message);
    }

    private void LogSentPacket(IMessage packet) {
        var scopedMessageName = packet
            .GetType()
            .ToString()
            .Split('.')[^1]
            .Replace('+', '.');
        if (!_suppressedPackets.Contains(packet.GetType())) {
            Logger.Verbose("SessionActor {SessionId} sent KiNP packet {ScopedMessageName}",
                Logger.Args(_sessionid, scopedMessageName));
        }
    }

}
