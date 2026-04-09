namespace VeggieAlly.WebAPI.Configuration;

/// <summary>
/// LINE 設定選項 (WebAPI 層)
/// </summary>
public sealed class LineSettings
{
    public const string SectionName = "Line";
    
    public required string ChannelSecret { get; init; }
    public required string ChannelAccessToken { get; init; }
    public required string ChannelId { get; init; }
    public string TenantId { get; init; } = "default";
    public string? LiffBaseUrl { get; init; }
}