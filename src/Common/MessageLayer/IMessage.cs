namespace Imlight.Common.MessageLayer;

public interface IMessage {
    public byte MessageOrder { get; }
    public byte ServiceId { get; }
    public byte AccessLevel { get; }
}
