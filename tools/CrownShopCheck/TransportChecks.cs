using Imcodec.MessageLayer;
using Imcodec.MessageLayer.Generated;
using Imlight.CoreLib.Shared.Networking;

static class TransportChecks {
    public static void Run() {
        IMessage[] messages = [
            new ControlMessageProtocol.SessionAccept { SessionId = 42, TimestampLower = 1234 },
            new GAME_5_PROTOCOL.MSG_ATTACH { UserID = 0UL, CharID = 0UL, ZoneName = "WizardCity/WC_Streets/WC_Unicorn", Location = "Start", LoginKey = "test" },
            new ControlMessageProtocol.KeepAlive { SessionId = 42, Milliseconds = 123 },
        ];
        var packets = messages.Select(MessageEncoder.Encode).ToArray();
        var bytes = packets.SelectMany(x => x).ToArray();
        // Every possible TCP boundary, including inside the header and attach body.
        for (var split = 0; split <= bytes.Length; split++) {
            var stream = new KiPacketStream();
            var frames = stream.Append(bytes.AsSpan(0, split)).Concat(stream.Append(bytes.AsSpan(split))).ToList();
            Verify(frames, packets);
        }
        var byteStream = new KiPacketStream();
        var byteFrames = bytes.SelectMany(b => byteStream.Append(new[] { b })).ToList();
        Verify(byteFrames, packets);
        foreach (var malformed in new byte[][] { [0, 0, 5, 0], [13, 240, 0, 0], [13, 240, 0, 128] }) {
            try { new KiPacketStream().Append(malformed); throw new Exception("Invalid frame accepted"); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine("PASS: TCP coalesced handshake/attach, every split boundary, bytewise delivery, multiple frames and invalid frame rejection.");
    }
    static void Verify(IReadOnlyList<byte[]> frames, byte[][] expected) {
        if (frames.Count != expected.Length) throw new Exception("TCP frame lost");
        for (var i = 0; i < expected.Length; i++)
            if (!frames[i].SequenceEqual(expected[i]) || MessageEncoder.Decode(frames[i])?.Count != 1)
                throw new Exception("TCP frame corrupted");
    }
}
