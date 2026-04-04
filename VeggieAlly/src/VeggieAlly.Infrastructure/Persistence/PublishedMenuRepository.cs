using System.Text.Json;
using Dapper;
using Npgsql;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Infrastructure.Persistence;

/// <summary>
/// 已發布菜單 Repository — Dapper + PostgreSQL 實作
/// </summary>
public sealed class PublishedMenuRepository : IPublishedMenuRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PublishedMenuRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<PublishedMenu?> GetByTenantAndDateAsync(string tenantId, DateOnly date, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        // 查詢菜單基本資訊
        var menuSql = """
            SELECT id, tenant_id, published_by, date, published_at 
            FROM published_menus 
            WHERE tenant_id = @TenantId AND date = @Date
            """;

        var menu = await connection.QueryFirstOrDefaultAsync(menuSql, new { TenantId = tenantId, Date = date });
        if (menu is null)
            return null;

        // 查詢菜單品項
        var itemsSql = """
            SELECT id, menu_id, name, is_new, buy_price, sell_price, 
                   original_qty, remaining_qty, unit, historical_avg_price
            FROM published_menu_items 
            WHERE menu_id = @MenuId AND tenant_id = @TenantId
            ORDER BY name
            """;

        var items = await connection.QueryAsync(itemsSql, new { MenuId = menu.id, TenantId = tenantId });

        return new PublishedMenu
        {
            Id = menu.id,
            TenantId = menu.tenant_id,
            PublishedByUserId = menu.published_by,
            Date = menu.date,
            PublishedAt = menu.published_at,
            Items = items.Select(item => new PublishedMenuItem
            {
                Id = item.id,
                MenuId = item.menu_id,
                Name = item.name,
                IsNew = item.is_new,
                BuyPrice = item.buy_price,
                SellPrice = item.sell_price,
                OriginalQty = item.original_qty,
                RemainingQty = item.remaining_qty,
                Unit = item.unit,
                HistoricalAvgPrice = item.historical_avg_price
            }).ToList()
        };
    }

    public async Task InsertAsync(PublishedMenu menu, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // 插入菜單基本資訊
            var menuSql = """
                INSERT INTO published_menus (id, tenant_id, published_by, date, published_at)
                VALUES (@Id, @TenantId, @PublishedByUserId, @Date, @PublishedAt)
                """;

            await connection.ExecuteAsync(menuSql, menu, transaction);

            // 插入菜單品項
            var itemSql = """
                INSERT INTO published_menu_items (
                    id, menu_id, tenant_id, name, is_new, buy_price, sell_price, 
                    original_qty, remaining_qty, unit, historical_avg_price
                )
                VALUES (
                    @Id, @MenuId, @TenantId, @Name, @IsNew, @BuyPrice, @SellPrice, 
                    @OriginalQty, @RemainingQty, @Unit, @HistoricalAvgPrice
                )
                """;

            foreach (var item in menu.Items)
            {
                await connection.ExecuteAsync(itemSql, new
                {
                    Id = item.Id,
                    MenuId = item.MenuId,
                    TenantId = menu.TenantId, // 為安全考量，使用菜單的 TenantId
                    Name = item.Name,
                    IsNew = item.IsNew,
                    BuyPrice = item.BuyPrice,
                    SellPrice = item.SellPrice,
                    OriginalQty = item.OriginalQty,
                    RemainingQty = item.RemainingQty,
                    Unit = item.Unit,
                    HistoricalAvgPrice = item.HistoricalAvgPrice
                }, transaction);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> DeductItemStockAsync(string tenantId, string itemId, int amount, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        // 樂觀鎖語意：只有當 remaining_qty >= amount 時才更新
        var sql = """
            UPDATE published_menu_items
            SET remaining_qty = remaining_qty - @Amount
            WHERE id = @ItemId AND tenant_id = @TenantId AND remaining_qty >= @Amount
            """;

        var affected = await connection.ExecuteAsync(sql, new 
        { 
            ItemId = itemId, 
            TenantId = tenantId, 
            Amount = amount 
        });

        return affected;
    }

    public async Task DeleteByTenantAndDateAsync(string tenantId, DateOnly date, CancellationToken ct = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        // CASCADE 將自動刪除關聯的 published_menu_items
        var sql = """
            DELETE FROM published_menus 
            WHERE tenant_id = @TenantId AND date = @Date
            """;

        await connection.ExecuteAsync(sql, new { TenantId = tenantId, Date = date });
    }
}