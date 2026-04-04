using System.Collections.Concurrent;
using System.Text.Json;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Draft;

namespace VeggieAlly.Infrastructure.Storage;

/// <summary>
/// In-Memory 草稿 Session 儲存實作
/// </summary>
public sealed class InMemoryDraftSessionStore : IDraftSessionStore
{
    private readonly ConcurrentDictionary<string, (string Json, DateTimeOffset Expiry)> _store = new();

    public Task<DraftMenuSession?> GetAsync(string tenantId, string lineUserId, DateOnly date, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, lineUserId, date);
        
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.Expiry < DateTimeOffset.UtcNow)
            {
                // 已過期，移除
                _store.TryRemove(key, out _);
                return Task.FromResult<DraftMenuSession?>(null);
            }

            try
            {
                var session = JsonSerializer.Deserialize<DraftMenuSession>(entry.Json, JsonOptions);
                return Task.FromResult<DraftMenuSession?>(session);
            }
            catch (JsonException)
            {
                // 反序列化失敗，移除損壞的資料
                _store.TryRemove(key, out _);
                return Task.FromResult<DraftMenuSession?>(null);
            }
        }

        return Task.FromResult<DraftMenuSession?>(null);
    }

    public Task SaveAsync(DraftMenuSession session, CancellationToken ct = default)
    {
        var key = BuildKey(session.TenantId, session.LineUserId, session.Date);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        var expiry = DateTimeOffset.UtcNow.AddHours(24);

        _store.AddOrUpdate(key, (json, expiry), (_, _) => (json, expiry));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string tenantId, string lineUserId, DateOnly date, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, lineUserId, date);
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    private static string BuildKey(string tenantId, string lineUserId, DateOnly date)
        => $"{tenantId}:draft:{lineUserId}:{date:yyyy-MM-dd}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}