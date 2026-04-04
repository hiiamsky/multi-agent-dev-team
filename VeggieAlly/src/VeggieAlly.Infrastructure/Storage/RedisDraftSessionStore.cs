using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Draft;

namespace VeggieAlly.Infrastructure.Storage;

/// <summary>
/// Redis 草稿 Session 儲存實作
/// </summary>
public sealed class RedisDraftSessionStore : IDraftSessionStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisDraftSessionStore> _logger;

    public RedisDraftSessionStore(IConnectionMultiplexer redis, ILogger<RedisDraftSessionStore> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<DraftMenuSession?> GetAsync(string tenantId, string lineUserId, DateOnly date, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, lineUserId, date);
        
        try
        {
            var json = await _database.StringGetAsync(key);
            if (!json.HasValue)
                return null;

            var session = JsonSerializer.Deserialize<DraftMenuSession>(json!.ToString(), JsonOptions);
            return session;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Redis 草稿反序列化失敗，Key: {Key}", key);
            // 移除損壞的資料
            await _database.KeyDeleteAsync(key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 讀取草稿失敗，Key: {Key}", key);
            return null;
        }
    }

    public async Task SaveAsync(DraftMenuSession session, CancellationToken ct = default)
    {
        var key = BuildKey(session.TenantId, session.LineUserId, session.Date);
        
        try
        {
            var json = JsonSerializer.Serialize(session, JsonOptions);
            await _database.StringSetAsync(key, json, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 儲存草稿失敗，Key: {Key}", key);
            throw;
        }
    }

    public async Task DeleteAsync(string tenantId, string lineUserId, DateOnly date, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, lineUserId, date);
        
        try
        {
            await _database.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis 刪除草稿失敗，Key: {Key}", key);
            throw;
        }
    }

    private static string BuildKey(string tenantId, string lineUserId, DateOnly date)
        => $"{tenantId}:draft:{lineUserId}:{date:yyyy-MM-dd}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}