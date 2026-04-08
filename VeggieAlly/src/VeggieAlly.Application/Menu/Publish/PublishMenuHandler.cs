using MediatR;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Menu.Publish;

/// <summary>
/// 發布菜單處理器 — 從草稿轉換為已發布菜單，並支援發布鎖定
/// </summary>
public sealed class PublishMenuHandler : IRequestHandler<PublishMenuCommand, PublishedMenu>
{
    private readonly IDraftSessionStore _draftStore;
    private readonly IPublishedMenuRepository _repository;
    private readonly IPublishedMenuCache _cache;

    public PublishMenuHandler(
        IDraftSessionStore draftStore, 
        IPublishedMenuRepository repository, 
        IPublishedMenuCache cache)
    {
        _draftStore = draftStore;
        _repository = repository;
        _cache = cache;
    }

    public async Task<PublishedMenu> Handle(PublishMenuCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().AddHours(8).DateTime);

        // 1. 檢查是否已經發布（Redis 快速路徑）
        var exists = await _cache.ExistsAsync(request.TenantId, today, cancellationToken);
        if (exists)
            throw new MenuAlreadyPublishedException();

        // 2. 取得草稿 Session
        var draftSession = await _draftStore.GetAsync(request.TenantId, request.LineUserId, today, cancellationToken);
        if (draftSession is null)
            throw new MenuNotPublishedException();

        // 3. 驗證草稿不得為空
        if (draftSession.Items.Count == 0)
            throw new InvalidOperationException("草稿菜單不得為空");

        // 4. 轉換為 PublishedMenu
        var publishedMenuId = Guid.NewGuid().ToString("N");
        var publishedMenu = new PublishedMenu
        {
            Id = publishedMenuId,
            TenantId = request.TenantId,
            PublishedByUserId = request.LineUserId,
            Date = today,
            PublishedAt = DateTimeOffset.UtcNow,
            Items = draftSession.Items.Select(draft => new PublishedMenuItem
            {
                Id = Guid.NewGuid().ToString("N"),
                MenuId = publishedMenuId, // 在初始設定式中設定
                Name = draft.Name,
                IsNew = draft.IsNew,
                BuyPrice = draft.BuyPrice,
                SellPrice = draft.SellPrice,
                OriginalQty = draft.Quantity,
                RemainingQty = draft.Quantity, // 初始庫存 = 原始数量
                Unit = draft.Unit,
                HistoricalAvgPrice = draft.HistoricalAvgPrice
            }).ToList()
        };

        // 5. 存儲到 DB
        await _repository.InsertAsync(publishedMenu, cancellationToken);

        // 6. write-through 到 Redis
        try
        {
            await _cache.SetAsync(publishedMenu, cancellationToken);
        }
        catch
        {
            // 快取失敗不影響主流程，但後續的 cache.ExistsAsync 可能不準
        }

        // 7. 刪除草稿（發布鎖定）
        await _draftStore.DeleteAsync(request.TenantId, request.LineUserId, today, cancellationToken);

        return publishedMenu;
    }
}