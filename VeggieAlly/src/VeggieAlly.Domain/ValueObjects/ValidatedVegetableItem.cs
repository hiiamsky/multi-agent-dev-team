namespace VeggieAlly.Domain.ValueObjects;

/// <summary>
/// 經過驗證的蔬菜品項 
/// </summary>
public record ValidatedVegetableItem(
    string Name,
    bool IsNew,
    decimal BuyPrice,
    decimal SellPrice,
    int Quantity,
    string Unit,
    decimal? HistoricalAvgPrice,
    ValidationResult Validation);