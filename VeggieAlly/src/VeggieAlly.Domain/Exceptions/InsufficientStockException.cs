namespace VeggieAlly.Domain.Exceptions;

/// <summary>
/// 庫存不足異常
/// </summary>
public sealed class InsufficientStockException : Exception
{
    public string ItemId { get; }
    
    public InsufficientStockException(string itemId)
        : base($"庫存不足: {itemId}")
    {
        ItemId = itemId;
    }
}