using Microsoft.AspNetCore.Authorization;
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
[AllowAnonymous] // 實際驗證由 [LiffAuth] ActionFilter 負責；FallbackPolicy 不介入此 Controller
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
    public async Task<IActionResult> PublishMenu(CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue("TenantId", out var tenantIdValue) ||
            !HttpContext.Items.TryGetValue("LineUserId", out var userIdValue) ||
            string.IsNullOrWhiteSpace(tenantIdValue?.ToString()) ||
            string.IsNullOrWhiteSpace(userIdValue?.ToString()))
        {
            _logger.LogWarning("Missing authentication context in {Action}", nameof(PublishMenu));
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Missing authentication information" } });
        }

        var tenantId = tenantIdValue.ToString()!;
        var userId = userIdValue.ToString()!;

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
    public async Task<IActionResult> UnpublishMenu(CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue("TenantId", out var tenantIdValue) ||
            string.IsNullOrWhiteSpace(tenantIdValue?.ToString()))
        {
            _logger.LogWarning("Missing authentication context in {Action}", nameof(UnpublishMenu));
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Missing authentication information" } });
        }

        var tenantId = tenantIdValue.ToString()!;

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
    public async Task<IActionResult> GetTodayMenu(CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue("TenantId", out var tenantIdValue) ||
            string.IsNullOrWhiteSpace(tenantIdValue?.ToString()))
        {
            _logger.LogWarning("Missing authentication context in {Action}", nameof(GetTodayMenu));
            return Unauthorized(new { error = new { code = "UNAUTHORIZED", message = "Missing authentication information" } });
        }

        var tenantId = tenantIdValue.ToString()!;

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