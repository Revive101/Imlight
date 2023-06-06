namespace Imlight.Server.Shared.Networking
{
    public interface IServerMessage
    {
        public byte MessageOrder { get; }
        public byte ServiceID { get; }
    }
}