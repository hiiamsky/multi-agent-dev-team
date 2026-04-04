using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Domain.Models.Draft;

/// <summary>
/// 草稿品項 — 與 ValidatedVegetableItem 結構相同，但額外包含 Id
/// </summary>
public sealed record DraftItem(
    string Id,                        // GUID "N" 格式（32 hex chars）
    string Name,
    bool IsNew,
    decimal BuyPrice,
    decimal SellPrice,
    int Quantity,
    string Unit,
    decimal? HistoricalAvgPrice,
    ValidationResult Validation);