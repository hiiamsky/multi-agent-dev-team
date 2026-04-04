namespace VeggieAlly.Domain.Models.Menu;

/// <summary>
/// 已發布菜單品項 — 包含庫存計數
/// </summary>
public sealed class PublishedMenuItem
{
    public required string Id { get; init; }              // GUID "N" 格式
    public required string MenuId { get; init; }
    public required string Name { get; init; }
    public bool IsNew { get; init; }
    public decimal BuyPrice { get; init; }
    public decimal SellPrice { get; init; }
    public int OriginalQty { get; init; }
    public int RemainingQty { get; set; }
    public required string Unit { get; init; }
    public decimal? HistoricalAvgPrice { get; init; }
}