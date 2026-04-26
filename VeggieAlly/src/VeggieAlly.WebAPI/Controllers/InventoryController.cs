using Microsoft.AspNetCore.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using VeggieAlly.Application.Menu.DeductInventory;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.WebAPI.Contracts.Inventory;
using VeggieAlly.WebAPI.Filters;

namespace VeggieAlly.WebAPI.Controllers;

[ApiController]
[Route("api/menu")]
[AllowAnonymous] // 實際驗證由 [LiffAuth] ActionFilter 負責；FallbackPolicy 不介入此 Controller
public sealed class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IMediator mediator, ILogger<InventoryController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// 庫存扣除
    /// </summary>
    [LiffAuth]
    [HttpPatch("inventory")]
    public async Task<IActionResult> DeductInventory(
        [FromBody] DeductInventoryRequest request,
        CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue("TenantId", out var tenantIdValue) ||
            string.IsNullOrWhiteSpace(tenantIdValue?.ToString()))
        {
            _logger.LogWarning("Missing authentication context in {Action}", nameof(DeductInventory));
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Missing authentication information" } });
        }

        var tenantId = tenantIdValue.ToString()!;

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .SelectMany(x => x.Value!.Errors)
                .Select(x => x.ErrorMessage)
                .ToArray();
            return BadRequest(new { error = new { code = "VALIDATION_FAILED", message = string.Join("; ", errors) } });
        }

        try
        {
            var command = new DeductInventoryCommand(tenantId, request.ItemId, request.Amount);
            var updatedItem = await _mediator.Send(command, ct);

            _logger.LogInformation("Inventory deducted for item {ItemId} in tenant {TenantId}, amount: {Amount}", 
                request.ItemId, tenantId, request.Amount);

            return Ok(new
            {
                id = updatedItem.Id,
                name = updatedItem.Name,
                remaining_qty = updatedItem.RemainingQty,
                unit = updatedItem.Unit
            });
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { error = new { code = "INSUFFICIENT_STOCK", message = ex.Message } });
        }
        catch (MenuNotPublishedException)
        {
            return NotFound(new { error = new { code = "MENU_NOT_FOUND", message = "今日尚未發布菜單" } });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_ARGUMENT", message = ex.Message } });
        }
    }
}