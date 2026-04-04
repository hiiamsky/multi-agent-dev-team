using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Services;

/// <summary>
/// 草稿選單服務實作
/// </summary>
public sealed class DraftMenuService : IDraftMenuService
{
    private readonly IDraftSessionStore _store;
    private readonly IPriceValidationService _validationService;
    private readonly IVegetablePricingService _pricingService;
    private readonly ILogger<DraftMenuService> _logger;

    public DraftMenuService(
        IDraftSessionStore store,
        IPriceValidationService validationService,
        IVegetablePricingService pricingService,
        ILogger<DraftMenuService> logger)
    {
        _store = store;
        _validationService = validationService;
        _pricingService = pricingService;
        _logger = logger;
    }

    public async Task<DraftMenuSession> CreateOrMergeDraftAsync(
        string tenantId, string lineUserId,
        IReadOnlyList<ValidatedVegetableItem> newItems,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().AddHours(8).DateTime);
        var existing = await _store.GetAsync(tenantId, lineUserId, today, ct);

        if (existing is null)
        {
            // 建立新 session
            var newSession = new DraftMenuSession
            {
                TenantId = tenantId,
                LineUserId = lineUserId,
                Date = today,
                Items = newItems.Select(item => CreateDraftItem(item)).ToList(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            await _store.SaveAsync(newSession, ct);
            return newSession;
        }
        else
        {
            // 合併邏輯：以 Name 匹配，覆寫或追加
            var mergedItems = new List<DraftItem>(existing.Items);

            foreach (var newItem in newItems)
            {
                var existingIndex = mergedItems.FindIndex(item => 
                    string.Equals(item.Name, newItem.Name, StringComparison.OrdinalIgnoreCase));

                if (existingIndex >= 0)
                {
                    // 覆寫，保留原 Id
                    var existingId = mergedItems[existingIndex].Id;
                    mergedItems[existingIndex] = CreateDraftItem(newItem, existingId);
                }
                else
                {
                    // 追加新品項
                    mergedItems.Add(CreateDraftItem(newItem));
                }
            }

            existing.Items.Clear();
            existing.Items.AddRange(mergedItems);
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await _store.SaveAsync(existing, ct);
            return existing;
        }
    }

    public async Task<DraftItem> CorrectItemPriceAsync(
        string tenantId, string lineUserId,
        string itemId, decimal? newBuyPrice, decimal? newSellPrice,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().AddHours(8).DateTime);
        var session = await _store.GetAsync(tenantId, lineUserId, today, ct);

        if (session is null)
            throw new InvalidOperationException("Draft session not found");

        var item = session.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            throw new KeyNotFoundException("Draft item not found");

        var effectiveBuyPrice = newBuyPrice ?? item.BuyPrice;
        var effectiveSellPrice = newSellPrice ?? item.SellPrice;

        var historicalAvg = await _pricingService.GetHistoricalAvgPriceAsync(item.Name, cancellationToken: ct);
        var validation = _validationService.Validate(effectiveBuyPrice, effectiveSellPrice, historicalAvg);

        var updatedItem = item with
        {
            BuyPrice = effectiveBuyPrice,
            SellPrice = effectiveSellPrice,
            HistoricalAvgPrice = historicalAvg,
            Validation = validation
        };

        // 替換 session 中的品項
        var itemIndex = session.Items.FindIndex(i => i.Id == itemId);
        session.Items[itemIndex] = updatedItem;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        await _store.SaveAsync(session, ct);
        return updatedItem;
    }

    public async Task<DraftMenuSession?> GetDraftAsync(
        string tenantId, string lineUserId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().AddHours(8).DateTime);
        return await _store.GetAsync(tenantId, lineUserId, today, ct);
    }

    private static DraftItem CreateDraftItem(ValidatedVegetableItem item, string? existingId = null)
    {
        return new DraftItem(
            Id: existingId ?? Guid.NewGuid().ToString("N"),
            Name: item.Name,
            IsNew: item.IsNew,
            BuyPrice: item.BuyPrice,
            SellPrice: item.SellPrice,
            Quantity: item.Quantity,
            Unit: item.Unit,
            HistoricalAvgPrice: item.HistoricalAvgPrice,
            Validation: item.Validation
        );
    }
}