using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// 已發布菜單 Repository — Dapper 實作
/// </summary>
public interface IPublishedMenuRepository
{
    /// <summary>
    /// 根據租戶 ID 和日期查詢已發布菜單
    /// </summary>
    Task<PublishedMenu?> GetByTenantAndDateAsync(string tenantId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// 插入已發布菜單（包含所有品項）
    /// </summary>
    Task InsertAsync(PublishedMenu menu, CancellationToken ct = default);

    /// <summary>
    /// 庫存扣除，使用樂觀鎖語意 — 在 remaining_qty >= amount 時才扣除
    /// </summary>
    /// <param name="tenantId">租戶 ID</param>
    /// <param name="itemId">品項 ID</param>
    /// <param name="amount">扣除數量</param>
    /// <returns>影響的列數（為 0 表示庫存不足）</returns>
    Task<int> DeductItemStockAsync(string tenantId, string itemId, int amount, CancellationToken ct = default);

    /// <summary>
    /// 刪除指定租戶的指定日期菜單
    /// </summary>
    Task DeleteByTenantAndDateAsync(string tenantId, DateOnly date, CancellationToken ct = default);
}