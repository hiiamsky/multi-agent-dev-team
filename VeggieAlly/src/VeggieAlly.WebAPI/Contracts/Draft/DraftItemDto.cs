using System.Text.Json.Serialization;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.WebAPI.Contracts.Draft;

/// <summary>
/// 草稿品項回應 DTO
/// </summary>
public class DraftItemDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("is_new")]
    public bool IsNew { get; set; }

    [JsonPropertyName("buy_price")]
    public decimal BuyPrice { get; set; }

    [JsonPropertyName("sell_price")]
    public decimal SellPrice { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit")]
    public required string Unit { get; set; }

    [JsonPropertyName("historical_avg_price")]
    public decimal? HistoricalAvgPrice { get; set; }

    [JsonPropertyName("validation")]
    public required ValidationResultDto Validation { get; set; }
}

/// <summary>
/// 驗證結果 DTO
/// </summary>
public class ValidationResultDto
{
    [JsonPropertyName("status")]
    public ValidationStatus Status { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}