using MediatR;
using Microsoft.AspNetCore.Mvc;
using VeggieAlly.Application.Menu.GetToday;
using VeggieAlly.Application.Menu.Publish;
using VeggieAlly.Application.Menu.Unpublish;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.WebAPI.Filters;

namespace VeggieAlly.WebAPI.Controllers;

[ApiController]
[Route("api/menu")]
public sealed class MenuController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MenuController> _logger;

    public MenuController(IMediator mediator, ILogger<MenuController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// 發布今日菜單
    /// </summary>
    [LiffAuth]
    [HttpPost("publish")]
    public async Task<IActionResult> PublishMenu(
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        [FromHeader(Name = "X-User-Id")] string? userId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { error = new { code = "MISSING_TENANT", message = "Missing tenant ID" } });

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = new { code = "MISSING_USER", message = "Missing user ID" } });

        try
        {
            var command = new PublishMenuCommand(tenantId, userId);
            var result = await _mediator.Send(command, ct);

            _logger.LogInformation("Menu published successfully for tenant {TenantId} by user {UserId}", 
                tenantId, userId);

            return CreatedAtAction(nameof(GetTodayMenu), new { tenant_id = tenantId }, 
                new
                {
                    id = result.Id,
                    tenant_id = result.TenantId,
                    date = result.Date.ToString("yyyy-MM-dd"),
                    published_at = result.PublishedAt,
                    items_count = result.Items.Count
                });
        }
        catch (MenuAlreadyPublishedException)
        {
            return Conflict(new { error = new { code = "ALREADY_PUBLISHED", message = "今日菜單已發布" } });
        }
        catch (MenuNotPublishedException)
        {
            return NotFound(new { error = new { code = "NO_DRAFT", message = "尚未建立草稿菜單" } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
        }
    }

    /// <summary>
    /// 撤回發布 (MVP: 回傳 501)
    /// </summary>
    [LiffAuth]
    [HttpDelete("publish")]
    public async Task<IActionResult> UnpublishMenu(
        [FromHeader(Name = "X-Tenant-Id")] string? tenantId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { error = new { code = "MISSING_TENANT", message = "Missing tenant ID" } });

        try
        {
            var command = new UnpublishMenuCommand(tenantId);
            await _mediator.Send(command, ct);
            return NoContent();
        }
        catch (NotImplementedException ex)
        {
            return StatusCode(501, new { error = new { code = "NOT_IMPLEMENTED", message = ex.Message } });
        }
    }

    /// <summary>
    /// 查詢今日已發布菜單
    /// </summary>
    [LiffAuth]
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayMenu(
        [FromQuery(Name = "tenant_id")] string? tenantId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { error = new { code = "MISSING_TENANT", message = "Missing tenant_id parameter" } });

        var query = new GetTodayMenuQuery(tenantId);
        var menu = await _mediator.Send(query, ct);

        if (menu is null)
            return NotFound(new { error = new { code = "MENU_NOT_FOUND", message = "今日尚未發布菜單" } });

        _logger.LogInformation("Today menu retrieved for tenant {TenantId}", tenantId);

        return Ok(new
        {
            id = menu.Id,
            tenant_id = menu.TenantId,
            date = menu.Date.ToString("yyyy-MM-dd"),
            published_at = menu.PublishedAt,
            items = menu.Items.Select(item => new
            {
                id = item.Id,
                name = item.Name,
                is_new = item.IsNew,
                sell_price = item.SellPrice,
                original_qty = item.OriginalQty,
                remaining_qty = item.RemainingQty,
                unit = item.Unit
            })
        });
    }
}