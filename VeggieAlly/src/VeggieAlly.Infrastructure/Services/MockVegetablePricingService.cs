using VeggieAlly.Application.Common.Interfaces;

namespace VeggieAlly.Infrastructure.Services;

/// <summary>
/// 蔬菜價格查詢模擬服務
/// </summary>
public sealed class MockVegetablePricingService : IVegetablePricingService
{
    private readonly Dictionary<string, decimal> _historicalPrices;

    public MockVegetablePricingService()
    {
        // 從 SystemPrompts.cs 裡面的品項清單挑選常見蔬菜的歷史均價
        _historicalPrices = new Dictionary<string, decimal>
        {
            ["初秋高麗菜"] = 26,
            ["改良高麗菜"] = 26,
            ["小白菜"] = 15,
            ["青江菜"] = 18,
            ["空心菜"] = 20,
            ["白蘿蔔"] = 12,
            ["紅蘿蔔"] = 22,
            ["牛番茄"] = 30,
            ["金針菇"] = 25,
            ["包心大白菜"] = 20,
            ["油菜"] = 16,
            ["莧菜"] = 18,
            ["菠菜"] = 22,
            ["A菜"] = 15,
            ["萵苣"] = 18,
            ["馬鈴薯"] = 15,
            ["本地洋蔥"] = 20,
            ["進口洋蔥"] = 18,
            ["芋頭"] = 35,
            ["老薑"] = 80,
            ["嫩薑"] = 60,
            ["蒜頭"] = 50,
            ["聖女番茄"] = 40,
            ["小黃瓜"] = 22,
            ["胡瓜"] = 20,
            ["絲瓜"] = 25,
            ["茄子"] = 28,
            ["青椒"] = 30,
            ["敏豆"] = 35,
            ["四季豆"] = 32,
            ["青花菜"] = 30,
            ["花椰菜"] = 28,
            ["甜玉米"] = 25,
            ["南瓜"] = 15,
            ["冬瓜"] = 10,
            ["蘆筍"] = 120,
            ["九層塔"] = 50,
            ["杏鮑菇"] = 40,
            ["生香菇"] = 60,
            ["秀珍菇"] = 35,
            ["鴻喜菇"] = 30,
            ["黑木耳"] = 80,
            ["香菜"] = 60,
            ["芹菜"] = 25,
            ["辣椒"] = 100
        };
    }

    /// <summary>
    /// 查詢過去 N 日同品項平均進價
    /// </summary>
    public Task<decimal?> GetHistoricalAvgPriceAsync(string itemName, int days = 7, CancellationToken cancellationToken = default)
    {
        // 檢查空白品項
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return Task.FromResult<decimal?>(null);
        }

        // 查詢歷史價格
        decimal? price = _historicalPrices.TryGetValue(itemName.Trim(), out var value) ? value : null;
        
        return Task.FromResult(price);
    }
}