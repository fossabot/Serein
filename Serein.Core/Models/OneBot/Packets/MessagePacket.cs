using Serein.Core.Models.OneBot.Messages;

namespace Serein.Core.Models.OneBot.Packets;

public class MessagePacket : Packet
{
    public MessageType MessageType { get; init; }

    public SubType SubType { get; init; }

    public string? Echo { get; init; }

    public long MessageId { get; init; }

    public long MessageSeq { get; init; }

    public Sender Sender { get; init; } = new();

    public long SelfId { get; init; }

    public long UserId { get; init; }

    public long GroupId { get; init; }

    public string RawMessage { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}