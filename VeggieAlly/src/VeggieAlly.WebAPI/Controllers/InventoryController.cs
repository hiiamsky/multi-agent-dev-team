using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using VeggieAlly.Application.Menu.DeductInventory;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.WebAPI.Filters;

namespace VeggieAlly.WebAPI.Controllers;

[ApiController]
[Route("api/menu")]
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
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        [FromBody] DeductInventoryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { error = new { code = "MISSING_TENANT", message = "Missing tenant ID" } });

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

/// <summary>
/// 庫存扣除請求 DTO
/// </summary>
public sealed record DeductInventoryRequest
{
    [Required(ErrorMessage = "Item ID is required")]
    [JsonPropertyName("item_id")]
    public required string ItemId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    [JsonPropertyName("amount")]
    public required int Amount { get; init; }
}