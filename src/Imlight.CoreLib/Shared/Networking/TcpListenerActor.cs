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
 * TCP LISTENER ACTOR
 * ========================================================================
 * 
 * PURPOSE:
 * Manages asynchronous TCP socket listening and new connection 
 * allocation for network servers, handling incoming socket connections.
 * 
 * USAGE EXAMPLE:
 * // TCP listener is typically created by a server instance
 * // Handles incoming network connection acceptance
 * 
 * NOTE:
 * 
 * TODO:
 * 
 * Created by: Jooty
 * Version: KALI 1.0
 * Last Updated: 10/25/2026
 */

using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Akka.Actor;
using Imlight.Common;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Networking;

public class TcpListenerActor : ReceiveActor {

    public string Name { get; }
    public int Port { get; }
    public bool Listening { get; private set; }
    public TcpListener Listener { get; }

    private readonly CancellationTokenSource _tokenSource;
    private readonly IActorRef _serverRef;

    public TcpListenerActor(string name, int port, IActorRef serverRef, bool startListening = true) {
        this.Name = name;
        this.Port = port;
        this.Listener = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
        this._tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
        this._serverRef = serverRef;

        Receive<SERVER_100_PROTOCOL.MSG_STARTLISTENING>(_ => {
            if (!Start()) {
                return;
            }

            Sender.Tell(new SERVER_100_PROTOCOL.MSG_STARTLISTENING_COMPLETE());
        });

        if (startListening) {
            Start();
        }
    }

    public static Props Props(string name, int port, IActorRef serverRef, bool startListening = true) 
        => Akka.Actor.Props.Create(() => new TcpListenerActor(name, port, serverRef, startListening));

    public bool Start() {
        if (Listening) {
            return true;
        }

        try {
            this.Listener.Start();
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse) {
            Logger.Fatal("Port {Port} ({Name}) is already in use by another program. Close that program, " +
                         "or change LoginServerPort or GameServerPort in Imlight.ini.", Logger.Args(Port, Name));
            ExitServer();

            return false;
        }
        catch (SocketException ex) {
            Logger.Fatal("{Name} could not listen on port {Port}: {Reason} Pick another port in Imlight.ini.",
                Logger.Args(Name, Port, ex.Message));
            ExitServer();

            return false;
        }

        this.Listening = true;

        Logger.Information("{Name} is listening on port {Port}.", Logger.Args(Name, Port));

        _ = ListenAsync(this._tokenSource.Token);

        return true;
    }

    public void Stop() {
        this.Listening = false;
        this._tokenSource.Cancel();
        Listener.Stop();

        Logger.Debug("TcpListener for {Name} on port {Port} stopped.",
            Logger.Args(Name, Port));
    }

    /// <summary>
    /// Asynchronously listen for incoming connections.
    /// </summary>
    protected async Task ListenAsync(CancellationToken token) {
        while (!token.IsCancellationRequested) {
            try {
                var socket = await Listener.AcceptSocketAsync(token);
                AllocateNewSocket(socket);
            }
            catch (OperationCanceledException) {
                break;
            }
            catch (ObjectDisposedException) {
                break;
            }
            catch (Exception ex) {
                Logger.Error("{Name} on port {Port} could not accept a connection. {Exception}",
                    Logger.Args(Name, Port, ex));
            }
        }
    }

    private void AllocateNewSocket(Socket socket) {
        Logger.Debug("TcpListener for {Name} accepted connection from {RemoteEndPoint}.",
            Logger.Args(Name, socket.RemoteEndPoint?.ToString()));

        try {
            socket.NoDelay = true;
        } catch (Exception ex) when (ex is SocketException or ObjectDisposedException) {
            Logger.Debug("TcpListener for {Name} could not set NoDelay on a socket. {Exception}",
                Logger.Args(Name, ex.Message));
        }

        var msg = new SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET() { Socket = socket };
        _serverRef.Tell(msg);
    }

    private static void ExitServer() {
        // Exits off the actor thread, so the actor system can shut down while the process ends.
        Environment.ExitCode = 1;
        _ = Task.Run(() => Environment.Exit(1));
    }

}
