/* Copyright (C) Revive101 Development Team - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential.
 */

using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Akka.Actor;
using Imlight.CoreLib.Shared.Packets;

namespace Imlight.CoreLib.Shared.Networking;

public class TcpListenerActor : ReceiveActor {
    public string Name { get; }
    public int Port { get; }
    public bool Listening { get; private set; }
    public TcpListener Listener { get; }

    private readonly CancellationTokenSource _tokenSource;
    private readonly IActorRef _serverRef;

    public TcpListenerActor(string name, int port, IActorRef serverRef) {
        this.Name = name;
        this.Port = port;
        this.Listener = new TcpListener(IPAddress.Parse("0.0.0.0"), port);
        this._tokenSource = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
        this._serverRef = serverRef;

        Start();
    }

    public static Props Props(string name, int port, IActorRef serverRef) {
        return Akka.Actor.Props.Create(() => new TcpListenerActor(name, port, serverRef));
    }

    public async void Start() {
        this.Listening = true;
        this.Listener.Start();

        var token = this._tokenSource.Token;

        await ListenAsync(token, Context);
    }

    public void Stop() {
        this.Listening = false;
        this._tokenSource.Cancel();
        Listener.Stop();
    }

    /// <summary>
    /// Asyncronously listen for incoming connections.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns></returns>
    protected async Task ListenAsync(CancellationToken token, IUntypedActorContext context) {
        // Listen for any incoming sockets and accept data they send.
        while (!token.IsCancellationRequested) {
            if (!this.Listening) {
                continue;
            }

            // Accept socket and create a new SessionActor for them.
            var socket = await Listener.AcceptSocketAsync();
            AllocateNewSocket(socket, context);
        }
    }

    private void AllocateNewSocket(Socket socket, IUntypedActorContext context) {
        var msg = new SERVER_100_PROTOCOL.MSG_ALLOCATESOCKET() { Socket = socket };
        _serverRef.Tell(msg);
    }
}
