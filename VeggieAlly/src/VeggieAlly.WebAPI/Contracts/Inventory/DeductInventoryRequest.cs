using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VeggieAlly.WebAPI.Contracts.Inventory;

/// <summary>
/// 庫存扣除請求 DTO
/// </summary>
public sealed record DeductInventoryRequest
{
    [Required(ErrorMessage = "Item ID is required")]
    [JsonPropertyName("item_id")]
    public required string ItemId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    [JsonPropertyName("amount")]
    public required int Amount { get; init; }
}