using VeggieAlly.Domain.Models.Draft;

namespace VeggieAlly.Domain.Abstractions;

/// <summary>
/// 草稿 Session 儲存抽象介面
/// </summary>
public interface IDraftSessionStore
{
    Task<DraftMenuSession?> GetAsync(
        string tenantId, string lineUserId, DateOnly date,
        CancellationToken ct = default);

    Task SaveAsync(DraftMenuSession session, CancellationToken ct = default);

    Task DeleteAsync(
        string tenantId, string lineUserId, DateOnly date,
        CancellationToken ct = default);
}