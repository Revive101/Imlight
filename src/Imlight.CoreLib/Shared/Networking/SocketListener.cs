/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using Akka.Actor;
using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Networking;

internal sealed class SocketListener : ReceiveActor, IDisposable {

    private readonly int _bufferSize = ConfigurationManager.Settings["SessionActorBufferSize"].AsInt();
    private readonly bool _closeOnSocketException = ConfigurationManager.Settings["SessionActorCloseOnException"].AsBool();
    private readonly int _tokenBucketMax = ConfigurationManager.Settings["SessionTokenBucketMax"].AsInt();
    private readonly int _tokenBucketPerSecond = ConfigurationManager.Settings["SessionTokenBucketPerSecond"].AsInt();
    private readonly byte _tokenBucketFailedAcquisitionLimit = ConfigurationManager.Settings["SessionTokenBucketFailedAcquisitionLimit"].AsByte();
    private readonly IActorRef _sessionActorRef;
    private readonly Socket _socket;
    private readonly ushort _sessionid;
    private readonly TokenBucket _tokenBucket;
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
    public SocketListener(IActorRef sessionActor, Socket socket, ushort sessionid) {
        this._sessionActorRef = sessionActor;
        this._socket = socket;
        this._sessionid = sessionid;
        this._tokenBucket = new TokenBucket(_tokenBucketMax, _tokenBucketPerSecond);

        Receive<string>(x => x == "Close", x => Dispose());

        StartReceive();
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

    private void StartReceive() {
        if (_isDisposed) {
            return;
        }

        _socket.ReceiveTimeout = 0; // No timeout for synchronous receives
        var buffer = new byte[_bufferSize];

        try {
            int bytesReceived = _socket.Receive(buffer);
            ProcessReceivedData(buffer, bytesReceived);
        }
        catch (SocketException ex) {
            if (_closeOnSocketException) {
                Logger.Error("SessionActor {Id} receive operation failed: {Message}", Logger.Args(_sessionid, ex.Message));
                this.Dispose();
            }
        }
    }

    private void ProcessReceivedData(byte[] buffer, int bytesReceived) {
        try {
            if (bytesReceived <= 0) {
                // If the bytes transferred is 0, the socket has disconnected.
                this.Dispose();
                
                return;
            }
            if (!_tokenBucket.TryAcquire()) {
                Logger.Warning("SessionActor {SessionId} failed to acquire token.", Logger.Args(_sessionid));

                // The rate limit was reached.
                var failedAcquisitionCount = _tokenBucket.GetFailedAcquisitionCount();
                if (failedAcquisitionCount >= _tokenBucketFailedAcquisitionLimit) {
                    // Log warning.
                    Logger.Warning("SessionActor {SessionId} failed to acquire token {FailedAcquisitionCount} times.",
                        Logger.Args(_sessionid, failedAcquisitionCount));

                    // The session has exceeded the failed acquisition limit. We'll dispose of the session.
                    this.Dispose();

                    return;
                }

                return;
            }

            var packets = GetPacketsFromBuffer(buffer, bytesReceived);
            if (packets is null) {
                Logger.Verbose("SessionActor {Id} received invalid packet.", Logger.Args(_sessionid));
                return;
            }

            foreach (var packet in packets) {
                LogReceivedPacket(packet);

                var msgPacket = new SERVER_100_PROTOCOL.MSG_RECEIVEDPACKET { Packet = packet };
                _sessionActorRef.Tell(msgPacket);
            }

        }
        catch {
            this.Dispose();
        }
        finally {
            StartReceive();
        }
    }

    private IMessage[] GetPacketsFromBuffer(byte[] buffer, int bytesReceived) {
        var bufferSpan = new ReadOnlySpan<byte>(buffer, 0, bytesReceived).ToArray();
        if (!IsKIPacket(bufferSpan)) {
            Logger.Debug("SessionActor {SessionId} received non-KINP packet", 
                Logger.Args(_sessionid));

            return null;
        }

        if (TryDeserializePacket(bufferSpan, out var records)) {
            return [.. records];
        }
        else {
            Logger.Error("SessionActor {SessionID} packet deserialize failed.", 
                Logger.Args(_sessionid));
        }

        // The packet failed to deserialize.
        return null;

    }

    private bool TryDeserializePacket(byte[] buffer, out IReadOnlyCollection<IMessage> messages) {
        try {
            messages = MessageEncoder.Decode(buffer);
            
            return true;
        }
        catch (Exception ex) {
            Logger.Error("SessionActor {SessionID} packet deserialize failed: {ExMessage}",
                Logger.Args(_sessionid, ex.InnerException?.Message ?? ex.Message));

            messages = null;

            return false;
        }
    }

    private bool IsKIPacket(byte[] buffer) {
        if (buffer.Length < 2) {
            return false;
        }

        return buffer.AsSpan()[..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 });
    }

    private void LogReceivedPacket(IMessage packet) {
        var scopedMessageName = packet
            .GetType()
            .ToString()
            .Split('.')[^1]
            .Replace('+', '.');
        if (!_suppressedPackets.Contains(packet.GetType())) {
            Logger.Verbose("SessionActor {SessionId} received KiNP packet {ScopedMessageName}",
                Logger.Args(_sessionid, scopedMessageName));
        }
    }

}
