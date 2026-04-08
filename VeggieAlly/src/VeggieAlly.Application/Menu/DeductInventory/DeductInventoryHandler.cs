using MediatR;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Menu.DeductInventory;

/// <summary>
/// 庫存扣除處理器 — 樂觀鎖 + write-through 快取
/// </summary>
public sealed class DeductInventoryHandler : IRequestHandler<DeductInventoryCommand, PublishedMenuItem>
{
    private readonly IPublishedMenuRepository _repository;
    private readonly IPublishedMenuCache _cache;

    public DeductInventoryHandler(IPublishedMenuRepository repository, IPublishedMenuCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    public async Task<PublishedMenuItem> Handle(DeductInventoryCommand request, CancellationToken cancellationToken)
    {
        // 驗證參數
        if (request.Amount <= 0)
            throw new ArgumentException("扣除數量必須大於 0", nameof(request.Amount));

        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().AddHours(8).DateTime);

        // 1. 庫存扣除（樂觀鎖）
        var affected = await _repository.DeductItemStockAsync(
            request.TenantId, 
            request.ItemId, 
            request.Amount, 
            cancellationToken);

        // 2. 如果 affected == 0，表示庫存不足
        if (affected == 0)
            throw new InsufficientStockException(request.ItemId);

        // 3. 重新讀取完整菜單（避免部分更新造成不一致）
        var updatedMenu = await _repository.GetByTenantAndDateAsync(request.TenantId, today, cancellationToken)
            ?? throw new MenuNotPublishedException();

        // 4. write-through 更新快取
        try
        {
            await _cache.SetAsync(updatedMenu, cancellationToken);
        }
        catch
        {
            // 快取更新失敗，不影響主流程
        }

        // 5. 回傳更新後的品項
        var updatedItem = updatedMenu.Items.FirstOrDefault(i => i.Id == request.ItemId)
            ?? throw new ArgumentException($"Item {request.ItemId} not found after deduction");
        return updatedItem;
    }
}