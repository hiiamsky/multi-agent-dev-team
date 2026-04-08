using MediatR;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Menu.GetToday;

/// <summary>
/// 今日菜單查詢處理器 — 首先查詢 Redis，再查詢 DB，找到後回填快取
/// </summary>
public sealed class GetTodayMenuHandler : IRequestHandler<GetTodayMenuQuery, PublishedMenu?>
{
    private readonly IPublishedMenuCache _cache;
    private readonly IPublishedMenuRepository _repository;

    public GetTodayMenuHandler(IPublishedMenuCache cache, IPublishedMenuRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    public async Task<PublishedMenu?> Handle(GetTodayMenuQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().AddHours(8).DateTime);

        // 1. 首先查詢 Redis 快取
        try
        {
            var cached = await _cache.GetAsync(request.TenantId, today, cancellationToken);
            if (cached is not null)
                return cached;
        }
        catch
        {
            // 快取失敗，直接查詢 DB
        }

        // 2. 查詢 DB
        var menu = await _repository.GetByTenantAndDateAsync(request.TenantId, today, cancellationToken);
        
        // 3. 如果找到，回填快取
        if (menu is not null)
        {
            try
            {
                await _cache.SetAsync(menu, cancellationToken);
            }
            catch
            {
                // 快取回填失敗，不影響結果
            }
        }

        return menu;
    }
}