using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// 已發布菜單 Redis 快取
/// </summary>
public interface IPublishedMenuCache
{
    /// <summary>
    /// 從快取取得已發布菜單
    /// </summary>
    Task<PublishedMenu?> GetAsync(string tenantId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 設定快取（write-through）
    /// </summary>
    Task SetAsync(PublishedMenu menu, CancellationToken ct = default);

    /// <summary>
    /// 移除快取
    /// </summary>
    Task RemoveAsync(string tenantId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 檢查快取是否存在（用於防止重複發布）
    /// </summary>
    Task<bool> ExistsAsync(string tenantId, DateOnly date, CancellationToken ct = default);
}