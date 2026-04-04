using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Menu.Publish;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.Models.Menu;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public sealed class PublishMenuHandlerTests
{
    private readonly IDraftSessionStore _draftStore = Substitute.For<IDraftSessionStore>();
    private readonly IPublishedMenuRepository _repository = Substitute.For<IPublishedMenuRepository>();
    private readonly IPublishedMenuCache _cache = Substitute.For<IPublishedMenuCache>();
    private readonly PublishMenuHandler _handler;

    public PublishMenuHandlerTests()
    {
        _handler = new PublishMenuHandler(_draftStore, _repository, _cache);
    }

    private static DraftMenuSession CreateDraftSession(int itemCount = 2) => new()
    {
        TenantId = "tenant-1",
        LineUserId = "user-1",
        Date = DateOnly.FromDateTime(DateTime.Today),
        Items = Enumerable.Range(1, itemCount).Select(i => new DraftItem(
            Id: Guid.NewGuid().ToString("N"),
            Name: $"品項{i}",
            IsNew: false,
            BuyPrice: 20m + i,
            SellPrice: 35m + i,
            Quantity: 10 * i,
            Unit: "箱",
            HistoricalAvgPrice: 22m,
            Validation: ValidationResult.Ok()
        )).ToList(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Publish_AlreadyPublished_ThrowsAlreadyPublished()
    {
        _cache.ExistsAsync("tenant-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new PublishMenuCommand("tenant-1", "user-1");

        await Assert.ThrowsAsync<MenuAlreadyPublishedException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_NoDraft_ThrowsMenuNotPublished()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DraftMenuSession?)null);

        var command = new PublishMenuCommand("tenant-1", "user-1");

        await Assert.ThrowsAsync<MenuNotPublishedException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_EmptyDraft_ThrowsInvalidOp()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var emptyDraft = CreateDraftSession(0);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(emptyDraft);

        var command = new PublishMenuCommand("tenant-1", "user-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Publish_Valid_InsertsDbAndCache()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftSession());

        var command = new PublishMenuCommand("tenant-1", "user-1");
        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).InsertAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_Valid_DeletesDraft()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftSession());

        var command = new PublishMenuCommand("tenant-1", "user-1");
        await _handler.Handle(command, CancellationToken.None);

        await _draftStore.Received(1).DeleteAsync("tenant-1", "user-1", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_Valid_ReturnsPublishedMenu()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftSession());

        var command = new PublishMenuCommand("tenant-1", "user-1");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.Equal("user-1", result.PublishedByUserId);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Publish_ItemsMapping_QuantityToOriginalAndRemaining()
    {
        var draft = CreateDraftSession(1);
        draft.Items[0] = draft.Items[0] with { Quantity = 42 };

        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(draft);

        var result = await _handler.Handle(new PublishMenuCommand("tenant-1", "user-1"), CancellationToken.None);

        var item = result.Items[0];
        Assert.Equal(42, item.OriginalQty);
        Assert.Equal(42, item.RemainingQty);
    }

    [Fact]
    public async Task Publish_SetsPublisherId()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftSession());

        var result = await _handler.Handle(new PublishMenuCommand("tenant-1", "user-abc"), CancellationToken.None);

        Assert.Equal("user-abc", result.PublishedByUserId);
    }

    [Fact]
    public async Task Publish_DbFails_DoesNotDeleteDraft()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftSession());
        _repository.InsertAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("DB error"));

        var command = new PublishMenuCommand("tenant-1", "user-1");

        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));
        await _draftStore.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_CacheFails_StillSucceeds()
    {
        _cache.ExistsAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _draftStore.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftSession());
        _cache.SetAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Redis down"));

        var command = new PublishMenuCommand("tenant-1", "user-1");
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        await _repository.Received(1).InsertAsync(Arg.Any<PublishedMenu>(), Arg.Any<CancellationToken>());
        await _draftStore.Received(1).DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }
}
