using FluentAssertions;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.ValueObjects;
using VeggieAlly.Infrastructure.Storage;

namespace VeggieAlly.Application.Tests;

public sealed class InMemoryDraftSessionStoreTests
{
    private readonly InMemoryDraftSessionStore _store = new();

    private const string TenantId = "default";
    private const string LineUserId = "U_test";
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static DraftMenuSession CreateSession(string tenantId = TenantId, string userId = LineUserId)
    {
        return new DraftMenuSession
        {
            TenantId = tenantId,
            LineUserId = userId,
            Date = Today,
            Items = new List<DraftItem>
            {
                new("id00000000000000000000000000000a", "初秋高麗菜", false, 25, 35, 50, "箱", 25m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task Save_ThenGet_ReturnsSame()
    {
        var session = CreateSession();
        await _store.SaveAsync(session);

        var result = await _store.GetAsync(TenantId, LineUserId, Today);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(TenantId);
        result.LineUserId.Should().Be(LineUserId);
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("初秋高麗菜");
    }

    [Fact]
    public async Task Get_NonExistent_ReturnsNull()
    {
        var result = await _store.GetAsync("nonexistent", "nobody", Today);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Delete_ThenGet_ReturnsNull()
    {
        var session = CreateSession();
        await _store.SaveAsync(session);
        await _store.DeleteAsync(TenantId, LineUserId, Today);

        var result = await _store.GetAsync(TenantId, LineUserId, Today);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Get_DifferentDate_ReturnsNull()
    {
        var session = CreateSession();
        await _store.SaveAsync(session);

        var yesterday = Today.AddDays(-1);
        var result = await _store.GetAsync(TenantId, LineUserId, yesterday);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Save_Overwrites()
    {
        var session1 = CreateSession();
        await _store.SaveAsync(session1);

        var session2 = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = Today,
            Items = new List<DraftItem>
            {
                new("id00000000000000000000000000000b", "青江菜", false, 18, 28, 30, "箱", 18m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _store.SaveAsync(session2);

        var result = await _store.GetAsync(TenantId, LineUserId, Today);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("青江菜");
    }
}