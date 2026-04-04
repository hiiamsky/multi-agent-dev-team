namespace VeggieAlly.Domain.Models.Draft;

/// <summary>
/// 草稿選單 Session — 每個使用者每天一筆
/// </summary>
public sealed class DraftMenuSession
{
    public required string TenantId { get; init; }
    public required string LineUserId { get; init; }
    public required DateOnly Date { get; init; }
    public required List<DraftItem> Items { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}