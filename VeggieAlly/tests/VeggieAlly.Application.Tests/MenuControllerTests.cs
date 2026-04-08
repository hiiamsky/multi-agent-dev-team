using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Menu.DeductInventory;
using VeggieAlly.Application.Menu.GetToday;
using VeggieAlly.Application.Menu.Publish;
using VeggieAlly.Application.Menu.Unpublish;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.Domain.Models.Menu;
using VeggieAlly.WebAPI.Controllers;

namespace VeggieAlly.Application.Tests;

public sealed class MenuControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<MenuController> _logger = Substitute.For<ILogger<MenuController>>();
    private readonly MenuController _controller;

    public MenuControllerTests()
    {
        _controller = new MenuController(_mediator, _logger);
        
        // 設置 HttpContext.Items 模擬認證資訊
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = "tenant-1";
        httpContext.Items["LineUserId"] = "user-1";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static PublishedMenu CreateMenu() => new()
    {
        Id = "menu-1",
        TenantId = "tenant-1",
        PublishedByUserId = "user-1",
        Date = DateOnly.FromDateTime(DateTime.Today),
        Items = [new PublishedMenuItem
        {
            Id = "item-1",
            MenuId = "menu-1",
            Name = "高麗菜",
            SellPrice = 35m,
            BuyPrice = 20m,
            OriginalQty = 50,
            RemainingQty = 50,
            Unit = "箱"
        }],
        PublishedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Publish_Valid_Returns201()
    {
        _mediator.Send(Arg.Any<PublishMenuCommand>(), Arg.Any<CancellationToken>())
            .Returns(CreateMenu());

        var result = await _controller.PublishMenu(CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task Publish_AlreadyPublished_Returns409()
    {
        _mediator.Send(Arg.Any<PublishMenuCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MenuAlreadyPublishedException());

        var result = await _controller.PublishMenu(CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Publish_NoDraft_Returns404()
    {
        _mediator.Send(Arg.Any<PublishMenuCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MenuNotPublishedException());

        var result = await _controller.PublishMenu(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetToday_Exists_Returns200()
    {
        _mediator.Send(Arg.Any<GetTodayMenuQuery>(), Arg.Any<CancellationToken>())
            .Returns(CreateMenu());

        var result = await _controller.GetTodayMenu(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetToday_NotExists_Returns404()
    {
        _mediator.Send(Arg.Any<GetTodayMenuQuery>(), Arg.Any<CancellationToken>())
            .Returns((PublishedMenu?)null);

        var result = await _controller.GetTodayMenu(CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetToday_EmptyTenantId_Returns401()
    {
        // 設置空的 HttpContext.Items 來模擬缺少認證資訊的情況
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var result = await _controller.GetTodayMenu(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Unpublish_Returns501()
    {
        _mediator.Send(Arg.Any<UnpublishMenuCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotImplementedException("撤回功能将在後續版本實作"));

        var result = await _controller.UnpublishMenu(CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, objResult.StatusCode);
    }
}
