using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Menu.DeductInventory;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.Domain.Models.Menu;
using VeggieAlly.WebAPI.Controllers;
using VeggieAlly.WebAPI.Contracts.Inventory;

namespace VeggieAlly.Application.Tests;

public sealed class InventoryControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<InventoryController> _logger = Substitute.For<ILogger<InventoryController>>();
    private readonly InventoryController _controller;

    public InventoryControllerTests()
    {
        _controller = new InventoryController(_mediator, _logger);
        
        // 設置 HttpContext.Items 模擬認證資訊
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = "tenant-1";
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Deduct_Valid_Returns200()
    {
        var updatedItem = new PublishedMenuItem
        {
            Id = "item-1",
            MenuId = "menu-1",
            Name = "高麗菜",
            RemainingQty = 48,
            Unit = "箱"
        };
        _mediator.Send(Arg.Any<DeductInventoryCommand>(), Arg.Any<CancellationToken>())
            .Returns(updatedItem);

        var request = new DeductInventoryRequest { ItemId = "item-1", Amount = 2 };
        var result = await _controller.DeductInventory(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Deduct_InsufficientStock_Returns409()
    {
        _mediator.Send(Arg.Any<DeductInventoryCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InsufficientStockException("item-1"));

        var request = new DeductInventoryRequest { ItemId = "item-1", Amount = 100 };
        var result = await _controller.DeductInventory(request, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Deduct_MissingTenantId_Returns401()
    {
        // 設置空的 HttpContext.Items 來模擬缺少認證資訊的情況
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var request = new DeductInventoryRequest { ItemId = "item-1", Amount = 2 };
        var result = await _controller.DeductInventory(request, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Deduct_InvalidArgument_Returns400()
    {
        _mediator.Send(Arg.Any<DeductInventoryCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ArgumentException("扣除數量必須大於 0"));

        var request = new DeductInventoryRequest { ItemId = "item-1", Amount = 1 };
        var result = await _controller.DeductInventory(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Deduct_MenuNotPublished_Returns404()
    {
        _mediator.Send(Arg.Any<DeductInventoryCommand>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MenuNotPublishedException());

        var request = new DeductInventoryRequest { ItemId = "item-1", Amount = 2 };
        var result = await _controller.DeductInventory(request, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
