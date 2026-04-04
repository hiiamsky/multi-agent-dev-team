using System.Text.Json;
using StackExchange.Redis;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Infrastructure.Cache;

/// <summary>
/// 已發布菜單 Redis 快取實作
/// </summary>
public sealed class PublishedMenuCache : IPublishedMenuCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly JsonSerializerOptions _jsonOptions;

    public PublishedMenuCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        
        // 使用 snake_case 命名策略
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<PublishedMenu?> GetAsync(string tenantId, DateOnly date, CancellationToken ct = default)
    {
        var key = GetCacheKey(tenantId, date);
        var json = await _database.StringGetAsync(key);
        
        if (!json.HasValue)
            return null;

        return JsonSerializer.Deserialize<PublishedMenu>(json.ToString(), _jsonOptions);
    }

    public async Task SetAsync(PublishedMenu menu, CancellationToken ct = default)
    {
        var key = GetCacheKey(menu.TenantId, menu.Date);
        var json = JsonSerializer.Serialize(menu, _jsonOptions);
        var ttl = CalculateTtlUntilEndOfDay();

        await _database.StringSetAsync(key, json, ttl);
    }

    public async Task RemoveAsync(string tenantId, DateOnly date, CancellationToken ct = default)
    {
        var key = GetCacheKey(tenantId, date);
        await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string tenantId, DateOnly date, CancellationToken ct = default)
    {
        var key = GetCacheKey(tenantId, date);
        return await _database.KeyExistsAsync(key);
    }

    private static string GetCacheKey(string tenantId, DateOnly date)
    {
        return $"{tenantId}:menu:published:{date:yyyy-MM-dd}";
    }

    /// <summary>
    /// 計算至當日 23:59:59 (台灣時間 UTC+8) 的剩餘秒數
    /// </summary>
    private static TimeSpan CalculateTtlUntilEndOfDay()
    {
        // 获取台灣時間的現在時間
        var taiwanTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        var nowInTaiwan = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, taiwanTimeZone);
        
        // 計算當日 23:59:59 的時間
        var endOfDay = nowInTaiwan.Date.AddDays(1).AddSeconds(-1);
        
        // 返回剩餘時間
        var timeSpan = endOfDay - nowInTaiwan;
        
        // 確保最少有 1 分鐘的 TTL
        return timeSpan.TotalSeconds > 60 ? timeSpan : TimeSpan.FromMinutes(1);
    }
}