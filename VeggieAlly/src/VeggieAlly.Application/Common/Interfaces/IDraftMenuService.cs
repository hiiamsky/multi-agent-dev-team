using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// 草稿選單服務介面
/// </summary>
public interface IDraftMenuService
{
    Task<DraftMenuSession> CreateOrMergeDraftAsync(
        string tenantId, string lineUserId,
        IReadOnlyList<ValidatedVegetableItem> newItems,
        CancellationToken ct = default);

    Task<DraftItem> CorrectItemPriceAsync(
        string tenantId, string lineUserId,
        string itemId, decimal? newBuyPrice, decimal? newSellPrice,
        CancellationToken ct = default);

    Task<DraftMenuSession?> GetDraftAsync(
        string tenantId, string lineUserId,
        CancellationToken ct = default);
}