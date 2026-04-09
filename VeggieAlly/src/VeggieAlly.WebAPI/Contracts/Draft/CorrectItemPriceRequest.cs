using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VeggieAlly.WebAPI.Contracts.Draft;

/// <summary>
/// 修正品項價格請求
/// </summary>
public class CorrectItemPriceRequest
{
    [JsonPropertyName("buy_price")]
    [Range(0.01, 99999.99, ErrorMessage = "進價必須在 0.01 到 99999.99 之間")]
    public decimal? BuyPrice { get; set; }

    [JsonPropertyName("sell_price")]
    [Range(0.01, 99999.99, ErrorMessage = "售價必須在 0.01 到 99999.99 之間")]
    public decimal? SellPrice { get; set; }
}