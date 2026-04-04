namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// 蔬菜價格查詢服務介面
/// </summary>
public interface IVegetablePricingService
{
    /// <summary>
    /// 查詢過去 N 日同品項平均進價
    /// </summary>
    Task<decimal?> GetHistoricalAvgPriceAsync(string itemName, int days = 7, CancellationToken cancellationToken = default);
}