namespace Imlight.Net.Messages
{
    public interface IServerProtocol
    {
        public byte ServiceID { get; }
        public string ProtocolType { get; }
        public int ProtocolVersion { get; }
        public string ProtocolDescription { get; }
    }
}