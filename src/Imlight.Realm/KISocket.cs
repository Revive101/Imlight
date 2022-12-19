using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Imlight.Common.Logger;
using System.Net.Sockets;
using System.IO;
using Imlight.IO;

namespace Imlight.Realm
{
    internal class KISocket : IDisposable
    {

        private const ushort BUFFER_SIZE = 4096;
        private const bool DISPOSE_ON_UNHANDLED_EXCEPTION = true;

        internal short ID { get; private set; }
        internal bool IsOpen { get; private set; }

        private readonly TcpServer _server;
        private readonly TcpClient _client;
        private readonly byte[] _buffer;

        // ctor
        public KISocket(TcpServer server, TcpClient client)
        {
            this._server = server;
            this._client = client;
            this._buffer = new byte[BUFFER_SIZE];
            this.ID = Common.RandomGen.SignedNumber<short>();

            IsOpen = false;
        }

        internal void OpenListen()
        {
            IsOpen = true;

            while (IsOpen)
            {
                try
                {
                    Listen();
                }
                catch (IOException)
                {
                    Log.Error($"Socket [{this.ID}] connection forcibly closed by remote host. Dropping client..");
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"Socket unhandled listen error: {ex.Message}");
                    if (DISPOSE_ON_UNHANDLED_EXCEPTION) break;
                }
            }

            Dispose();
        }

        internal void Close() => this.IsOpen = false;

        private void Listen()
        {
            if (!IsOpen) return;

            using var stream = _client.GetStream();

            // Loop through all incoming data
            int i;
            while ((i = stream.Read(_buffer, 0, _buffer.Length)) != 0 && IsOpen)
            {
                // Translate data bytes to a ASCII string for logging.
                string data = Encoding.ASCII.GetString(_buffer, 0, i);
                Log.Debug($"Received data from socket ID [{ID}]: {data}");

                if (!KiNPBinaryReader.IsKiNPPacket(_buffer)) continue;

                // Trim unnecessary data.
                byte[] wizardBuffer = CreateKiNPBuffer(_buffer);

                SendPacketToEngine(wizardBuffer);
            }
        }

        private byte[] CreateKiNPBuffer(byte[] rawPacket)
        {
            // The original NetworkStream packet is put into a buffer of arbitrary length.
            // This is sometimes way more data than necessary. This method exists to shorten the raw
            // packet into a more appropriate size.

            Stream stream = new MemoryStream(rawPacket);
            BinaryReader reader = new BinaryReader(stream);

            // Skip KINP packet header.
            reader.BaseStream.Position += 2;

            byte[] wizardPacket;

            // The next part is the length, which changes sizes depending on the size of this packet.
            // If the packet is over the size of a uint_16, the following 4 bytes will be a uint_32, which is the actual length.
            UInt16 size = reader.ReadUInt16();
            if (size < 0x777F)
            {
                // This is a small packet.
                // The +4 comes from the packet header itself, which is not included in the size bytes.
                wizardPacket = new byte[size + 4];

                // Copy the raw packet into the shiny new packet. Skip the 4 original bytes as that data is no longer necessary.
                Array.Copy(rawPacket, 0, wizardPacket, 0, size);
            }
            else
            {
                // This is a large packet.
                // The +4 comes from the packet header itself, which is not included in the size bytes.
                UInt32 bigSize = reader.ReadUInt32();
                wizardPacket = new byte[bigSize + 4];

                // Copy the raw packet into the shiny new packet. Skip the 8 original bytes as that data is no longer necessary.
                Array.Copy(rawPacket, 0, wizardPacket, 0, bigSize);
            }

            stream.Dispose();
            reader.Dispose();

            return wizardPacket;
        }

        private void SendPacketToEngine(byte[] packet)
        {
            // Craft context and send to the engine for handling.
            var realmId = this._server.Realm.Id;
            Engine.WizardMessageContext context = new(packet, realmId, ID);
            Engine.WorkloadPool.Enqueue(context);
        }

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
