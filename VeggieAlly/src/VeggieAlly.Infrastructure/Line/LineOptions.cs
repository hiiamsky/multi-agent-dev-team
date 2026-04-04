namespace VeggieAlly.Infrastructure.Line;

public sealed class LineOptions
{
    public required string ChannelSecret { get; init; }
    public required string ChannelAccessToken { get; init; }
}