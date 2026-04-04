using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Draft.CorrectItem;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.ValueObjects;
using VeggieAlly.Infrastructure.Line;
using VeggieAlly.WebAPI.Controllers;

namespace VeggieAlly.Application.Tests;

public sealed class DraftControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<DraftController> _logger = Substitute.For<ILogger<DraftController>>();
    private readonly DraftController _controller;

    public DraftControllerTests()
    {
        var lineOptions = Options.Create(new LineOptions
        {
            ChannelSecret = "test-secret",
            ChannelAccessToken = "test-token",
            TenantId = "default"
        });
        _controller = new DraftController(_mediator, lineOptions, _logger);

        // 模擬 HttpContext Items（LiffAuth filter 設定的）
        var httpContext = new DefaultHttpContext();
        httpContext.Items["LineUserId"] = "U123";
        httpContext.Items["TenantId"] = "default";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static DraftItem CreateDraftItem(string id = "a1b2c3d4e5f67890a1b2c3d4e5f67890")
    {
        return new DraftItem(id, "初秋高麗菜", false, 25m, 35m, 50, "箱", 25m, ValidationResult.Ok());
    }

    // --- 11.4 #1 ---
    [Fact]
    public async Task Patch_Valid_Returns200()
    {
        var itemId = "a1b2c3d4e5f67890a1b2c3d4e5f67890";
        _mediator.Send(Arg.Any<CorrectDraftItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreateDraftItem(itemId));

        var result = await _controller.CorrectItemPrice(
            itemId,
            new CorrectItemPriceRequest { BuyPrice = 25m, SellPrice = 35m });

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    // --- 11.4 #2 ---
    [Fact]
    public async Task Patch_BothNull_Returns400()
    {
        var result = await _controller.CorrectItemPrice(
            "a1b2c3d4e5f67890a1b2c3d4e5f67890",
            new CorrectItemPriceRequest { BuyPrice = null, SellPrice = null });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- 11.4 #3 ---
    [Fact]
    public async Task Patch_NegativePrice_Returns400()
    {
        var result = await _controller.CorrectItemPrice(
            "a1b2c3d4e5f67890a1b2c3d4e5f67890",
            new CorrectItemPriceRequest { BuyPrice = -5m, SellPrice = 35m });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // --- 11.4 #4 ---
    [Fact]
    public async Task Patch_DraftNotFound_Returns404()
    {
        _mediator.Send(Arg.Any<CorrectDraftItemCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Draft session not found"));

        var result = await _controller.CorrectItemPrice(
            "a1b2c3d4e5f67890a1b2c3d4e5f67890",
            new CorrectItemPriceRequest { BuyPrice = 25m });

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
    }

    // --- 11.4 #5 ---
    [Fact]
    public async Task Patch_ItemNotFound_Returns404()
    {
        _mediator.Send(Arg.Any<CorrectDraftItemCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeyNotFoundException("Draft item not found"));

        var result = await _controller.CorrectItemPrice(
            "a1b2c3d4e5f67890a1b2c3d4e5f67890",
            new CorrectItemPriceRequest { SellPrice = 35m });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    // --- 11.4 #6 ---
    [Fact]
    public async Task Patch_InvalidIdFormat_Returns400()
    {
        var result = await _controller.CorrectItemPrice(
            "not-a-valid-guid",
            new CorrectItemPriceRequest { BuyPrice = 25m });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Patch_TooManyDecimalPlaces_Returns400()
    {
        var result = await _controller.CorrectItemPrice(
            "a1b2c3d4e5f67890a1b2c3d4e5f67890",
            new CorrectItemPriceRequest { BuyPrice = 25.123m });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
