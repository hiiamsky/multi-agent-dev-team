namespace VeggieAlly.Domain.Models.Parsing;

public sealed record ParsedMenuItem(
    string Name,
    bool IsNew,
    decimal BuyPrice,
    decimal SellPrice,
    int Quantity,
    string Unit);