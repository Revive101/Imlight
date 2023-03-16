namespace Imlight.Net.Messages
{
    public interface IServerMessage
    {
        public byte MessageOrder { get; }
        public byte ServiceID { get; }
    }
}