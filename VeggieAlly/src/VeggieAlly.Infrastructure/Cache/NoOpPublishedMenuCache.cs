using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Infrastructure.Cache;

/// <summary>
/// Redis 不可用時的 NoOp 實作，所有操作皆 miss/no-op
/// </summary>
public sealed class NoOpPublishedMenuCache : IPublishedMenuCache
{
    public Task<PublishedMenu?> GetAsync(string tenantId, DateOnly date, CancellationToken ct = default)
        => Task.FromResult<PublishedMenu?>(null);

    public Task SetAsync(PublishedMenu menu, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string tenantId, DateOnly date, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<bool> ExistsAsync(string tenantId, DateOnly date, CancellationToken ct = default)
        => Task.FromResult(false);
}
