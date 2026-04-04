namespace VeggieAlly.Domain.Models.Menu;

/// <summary>
/// 已發布菜單 — 每日每租戶一筆
/// </summary>
public sealed class PublishedMenu
{
    public required string Id { get; init; }              // GUID "N" 格式
    public required string TenantId { get; init; }
    public required string PublishedByUserId { get; init; }
    public required DateOnly Date { get; init; }
    public required List<PublishedMenuItem> Items { get; init; }
    public required DateTimeOffset PublishedAt { get; init; }
}