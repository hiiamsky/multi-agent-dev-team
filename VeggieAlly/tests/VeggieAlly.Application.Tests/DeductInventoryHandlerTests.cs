using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Menu.DeductInventory;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Tests;

public sealed class DeductInventoryHandlerTests
{
    private readonly IPublishedMenuRepository _repository = Substitute.For<IPublishedMenuRepository>();
    private readonly IPublishedMenuCache _cache = Substitute.For<IPublishedMenuCache>();
    private readonly DeductInventoryHandler _handler;

    public DeductInventoryHandlerTests()
    {
        _handler = new DeductInventoryHandler(_repository, _cache);
    }

    private static PublishedMenu CreateMenuWithItem(string itemId, int remainingQty = 50) => new()
    {
        Id = "menu-1",
        TenantId = "tenant-1",
        PublishedByUserId = "user-1",
        Date = DateOnly.FromDateTime(DateTime.Today),
        Items = [new PublishedMenuItem
        {
            Id = itemId,
            MenuId = "menu-1",
            Name = "高麗菜",
            IsNew = false,
            BuyPrice = 20m,
            SellPrice = 35m,
            OriginalQty = 50,
            RemainingQty = remainingQty,
            Unit = "箱"
        }],
        PublishedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Deduct_Valid_UpdatesDb()
    {
        _repository.DeductItemStockAsync("tenant-1", "item-1", 5, Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.GetByTenantAndDateAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateMenuWithItem("item-1", 45));

        var command = new DeductInventoryCommand("tenant-1", "item-1", 5);
        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).DeductItemStockAsync("tenant-1", "item-1", 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deduct_Valid_UpdatesCache()
    {
        _repository.DeductItemStockAsync("tenant-1", "item-1", 2, Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.GetByTenantAndDateAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateMenuWithItem("item-1", 48));

        var command = new DeductInventoryCommand("tenant-1", "item-1", 2);
        await _handler.Handle(command, CancellationToken.None);

        await _cache.Received(1).SetAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deduct_InsufficientStock_Throws()
    {
        _repository.DeductItemStockAsync("tenant-1", "item-1", 100, Arg.Any<CancellationToken>())
            .Returns(0);

        var command = new DeductInventoryCommand("tenant-1", "item-1", 100);

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Equal("item-1", ex.ItemId);
    }

    [Fact]
    public async Task Deduct_ZeroAmount_ThrowsValidation()
    {
        var command = new DeductInventoryCommand("tenant-1", "item-1", 0);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Deduct_NegativeAmount_ThrowsValidation()
    {
        var command = new DeductInventoryCommand("tenant-1", "item-1", -5);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Deduct_CacheFails_StillSucceeds()
    {
        _repository.DeductItemStockAsync("tenant-1", "item-1", 3, Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.GetByTenantAndDateAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateMenuWithItem("item-1", 47));
        _cache.SetAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Redis down"));

        var command = new DeductInventoryCommand("tenant-1", "item-1", 3);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(47, result.RemainingQty);
    }

    [Fact]
    public async Task Deduct_ReturnsUpdatedItem()
    {
        _repository.DeductItemStockAsync("tenant-1", "item-1", 2, Arg.Any<CancellationToken>())
            .Returns(1);
        _repository.GetByTenantAndDateAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateMenuWithItem("item-1", 48));

        var result = await _handler.Handle(
            new DeductInventoryCommand("tenant-1", "item-1", 2), CancellationToken.None);

        Assert.Equal("item-1", result.Id);
        Assert.Equal(48, result.RemainingQty);
    }
}
