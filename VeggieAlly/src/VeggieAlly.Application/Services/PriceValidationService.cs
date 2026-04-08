using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Services;

/// <summary>
/// 價格防呆驗證服務實作
/// </summary>
public sealed class PriceValidationService : IPriceValidationService
{
    /// <summary>
    /// 執行商業防呆規則
    /// </summary>
    public ValidationResult Validate(decimal buyPrice, decimal sellPrice, decimal? historicalAvgPrice)
    {
        // 規則 1（優先）：sellPrice <= buyPrice → Anomaly（但 sellPrice == 0 表示未設定售價，不判定為異常）
        if (sellPrice > 0 && sellPrice <= buyPrice)
        {
            return ValidationResult.Anomaly("售價低於或等於進價");
        }

        // 規則 2：與歷史均價落差 > 30% → Anomaly
        if (historicalAvgPrice.HasValue && historicalAvgPrice.Value > 0)
        {
            var deviation = Math.Abs(buyPrice - historicalAvgPrice.Value) / historicalAvgPrice.Value;
            if (deviation > 0.30m)
            {
                return ValidationResult.Anomaly($"與歷史均價落差 {deviation:P0}");
            }
        }

        // 規則 3：通過 → Ok
        return ValidationResult.Ok();
    }
}