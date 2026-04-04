using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Draft.CorrectItem;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public sealed class CorrectDraftItemHandlerTests
{
    private readonly IDraftMenuService _draftMenuService = Substitute.For<IDraftMenuService>();
    private readonly CorrectDraftItemHandler _handler;

    public CorrectDraftItemHandlerTests()
    {
        _handler = new CorrectDraftItemHandler(_draftMenuService);
    }

    [Fact]
    public async Task Handle_Valid_DelegatesAndReturns()
    {
        var expected = new DraftItem(
            "abc12345678901234567890123456789", "初秋高麗菜", false,
            25m, 35m, 50, "箱", 25m, ValidationResult.Ok());
        _draftMenuService.CorrectItemPriceAsync("default", "U123", "abc12345678901234567890123456789", 25m, 35m, Arg.Any<CancellationToken>())
            .Returns(expected);

        var command = new CorrectDraftItemCommand("default", "U123", "abc12345678901234567890123456789", 25m, 35m);
        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().Be(expected);
        await _draftMenuService.Received(1).CorrectItemPriceAsync("default", "U123", "abc12345678901234567890123456789", 25m, 35m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotFound_Propagates()
    {
        _draftMenuService.CorrectItemPriceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal?>(), Arg.Any<decimal?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeyNotFoundException("Draft item not found"));

        var command = new CorrectDraftItemCommand("default", "U123", "nonexistent", 25m, 35m);
        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
