using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Menu.GetToday;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Tests;

public sealed class GetTodayMenuHandlerTests
{
    private readonly IPublishedMenuCache _cache = Substitute.For<IPublishedMenuCache>();
    private readonly IPublishedMenuRepository _repository = Substitute.For<IPublishedMenuRepository>();
    private readonly GetTodayMenuHandler _handler;

    public GetTodayMenuHandlerTests()
    {
        _handler = new GetTodayMenuHandler(_cache, _repository);
    }

    private static PublishedMenu CreateMenu() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        TenantId = "tenant-1",
        PublishedByUserId = "user-1",
        Date = DateOnly.FromDateTime(DateTime.Today),
        Items = [new PublishedMenuItem
        {
            Id = Guid.NewGuid().ToString("N"),
            MenuId = "menu-1",
            Name = "高麗菜",
            IsNew = false,
            BuyPrice = 20m,
            SellPrice = 35m,
            OriginalQty = 50,
            RemainingQty = 48,
            Unit = "箱"
        }],
        PublishedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Get_CacheHit_ReturnsFromCache()
    {
        var menu = CreateMenu();
        _cache.GetAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(menu);

        var result = await _handler.Handle(new GetTodayMenuQuery("tenant-1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(menu.Id, result!.Id);
        await _repository.DidNotReceive().GetByTenantAndDateAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_CacheMiss_DbHit_ReturnAndBackfill()
    {
        var menu = CreateMenu();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublishedMenu?)null);
        _repository.GetByTenantAndDateAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(menu);

        var result = await _handler.Handle(new GetTodayMenuQuery("tenant-1"), CancellationToken.None);

        Assert.NotNull(result);
        await _cache.Received(1).SetAsync(Arg.Is<PublishedMenu>(m => m.Id == menu.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_CacheMissDbMiss_ReturnsNull()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublishedMenu?)null);
        _repository.GetByTenantAndDateAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublishedMenu?)null);

        var result = await _handler.Handle(new GetTodayMenuQuery("tenant-1"), CancellationToken.None);

        Assert.Null(result);
        await _cache.DidNotReceive().SetAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_CacheThrows_FallsBackToDb()
    {
        var menu = CreateMenu();
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Redis failure"));
        _repository.GetByTenantAndDateAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(menu);

        var result = await _handler.Handle(new GetTodayMenuQuery("tenant-1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(menu.Id, result!.Id);
    }

    [Fact]
    public async Task Get_CorrectTenantIsolation_PassesTenantIdToAll()
    {
        _cache.GetAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublishedMenu?)null);
        _repository.GetByTenantAndDateAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublishedMenu?)null);

        await _handler.Handle(new GetTodayMenuQuery("tenant-xyz"), CancellationToken.None);

        await _cache.Received(1).GetAsync("tenant-xyz", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).GetByTenantAndDateAsync("tenant-xyz", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
