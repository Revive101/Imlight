using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Imlight.Common;
using System.Buffers;
using System.Threading;
using WizUnraveler;
using WizUnraveler.DML;

namespace Imlight.Realm
{
    internal class KISocket : IDisposable
    {

        private const int BUFFER_SIZE = 4096;
        private const bool DISPOSE_ON_UNHANDLED_EXCEPTION = true;

        internal short ID { get; private set; }
        internal bool IsOpen { get; private set; }

        private readonly TcpServer _server;
        private readonly TcpClient _client;
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        // ctor
        public KISocket(TcpServer server, TcpClient client)
        {
            this._server = server;
            this._client = client;
            this.ID = RandomGen.SignedNumber<short>();

            IsOpen = false;
        }

        internal async Task OpenListenAsync()
        {
            IsOpen = true;
            while (IsOpen)
            {
                try
                {
                    await ListenAsync();
                }
                catch (IOException)
                {
                    Log.Logger.Error($"Socket [{this.ID}] connection forcibly closed by remote host. Dropping client..");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"Socket unhandled listen error: {ex.Message}");
                    if (DISPOSE_ON_UNHANDLED_EXCEPTION) break;
                }
            }

            Dispose();
        }

        internal void Close() => this.IsOpen = false;

        private async Task ListenAsync()
        {
            if (!IsOpen) return;

            using var stream = _client.GetStream();
            var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);

            try
            {
                while (IsOpen)
                {
                    int bytesRead = await stream.ReadAsync(buffer);
                    if (bytesRead == 0) break;

                    var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Log.Logger.Verbose($"Received data from socket ID [{ID}]: {data}");

                    if (!IsKIPacket(buffer)) continue;

                    SendPacketToEngine(buffer);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task SendAsync(INetworkMessage message)
        {
            if (!IsOpen)
            {
                Log.Logger.Error("Cannot send a packet on an unopened socket!");
                return;
            }

            await _semaphoreSlim.WaitAsync();
            try
            {
                var deserializedMessage = MessageSerializer.SerializeMessageBinary(message);
                var stream = _client.GetStream();
                await stream.WriteAsync(deserializedMessage);
                await stream.FlushAsync();
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private void SendPacketToEngine(byte[] packet)
        {
            // Craft context and send to the engine for handling.
            var realmId = this._server.Realm.Id;
            Engine.WizardMessageContext context = new(packet, realmId, ID);
            Engine.WorkloadPool.Enqueue(context);
        }

        private bool IsKIPacket(byte[] buffer)
            => (buffer.AsSpan()[0..2].SequenceEqual(stackalloc byte[2] { 0x0D, 0xF0 }));

        // dtor
        ~KISocket()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            IsOpen = false;
            _client.GetStream().Close();
            _client.Close();
            // Remove this object from the TCP server it's a part of.
            _server.Sockets.Remove(this);
            GC.SuppressFinalize(this);
        }

    }
}
