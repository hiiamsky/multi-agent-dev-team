namespace VeggieAlly.Infrastructure.Line;

public sealed class LineOptions
{
    public required string ChannelSecret { get; init; }
    public required string ChannelAccessToken { get; init; }
    public string TenantId { get; init; } = "default";
    public string? LiffBaseUrl { get; init; }
}