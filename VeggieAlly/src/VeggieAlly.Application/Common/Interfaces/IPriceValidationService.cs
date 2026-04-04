using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// 價格防呆驗證器介面
/// </summary>
public interface IPriceValidationService
{
    /// <summary>
    /// 執行商業防呆規則
    /// </summary>
    ValidationResult Validate(decimal buyPrice, decimal sellPrice, decimal? historicalAvgPrice);
}