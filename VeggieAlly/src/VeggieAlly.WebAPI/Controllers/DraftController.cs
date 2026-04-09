using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using VeggieAlly.Application.Draft.CorrectItem;
using VeggieAlly.WebAPI.Contracts.Draft;
using VeggieAlly.WebAPI.Filters;

namespace VeggieAlly.WebAPI.Controllers;

/// <summary>
/// 草稿管理 API Controller
/// </summary>
[ApiController]
[Route("api/draft")]
public class DraftController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DraftController> _logger;

    public DraftController(IMediator mediator, ILogger<DraftController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// 修正草稿品項價格
    /// </summary>
    [HttpPatch("item/{id}")]
    [LiffAuth]
    public async Task<ActionResult<DraftItemDto>> CorrectItemPrice(
        string id,
        [FromBody] CorrectItemPriceRequest request,
        CancellationToken cancellationToken = default)
    {
        // 驗證 ID 格式
        if (!IsValidGuidId(id))
        {
            return BadRequest(new { error = "INVALID_REQUEST", message = "無效的品項 ID 格式" });
        }

        // 驗證至少有一個價格
        if (request.BuyPrice is null && request.SellPrice is null)
        {
            return BadRequest(new { error = "INVALID_REQUEST", message = "至少須提供 buy_price 或 sell_price" });
        }

        // 驗證小數位數 ≤ 2
        if (!IsValidDecimalPlaces(request.BuyPrice) || !IsValidDecimalPlaces(request.SellPrice))
        {
            return BadRequest(new { error = "INVALID_REQUEST", message = "價格小數位數不得超過 2 位" });
        }

        // 驗證價格範圍
        if ((request.BuyPrice.HasValue && (request.BuyPrice.Value < 0.01m || request.BuyPrice.Value > 99999.99m)) ||
            (request.SellPrice.HasValue && (request.SellPrice.Value < 0.01m || request.SellPrice.Value > 99999.99m)))
        {
            return BadRequest(new { error = "INVALID_REQUEST", message = "價格必須在 0.01 到 99999.99 之間" });
        }

        try
        {
            if (!HttpContext.Items.TryGetValue("LineUserId", out var lineUserIdValue) ||
                !HttpContext.Items.TryGetValue("TenantId", out var tenantIdValue) ||
                string.IsNullOrWhiteSpace(lineUserIdValue?.ToString()) ||
                string.IsNullOrWhiteSpace(tenantIdValue?.ToString()))
            {
                _logger.LogWarning(
                    "Missing authentication context in {Action}. LineUserId present: {HasLineUserId}, TenantId present: {HasTenantId}",
                    nameof(CorrectItemPrice),
                    HttpContext.Items.ContainsKey("LineUserId"),
                    HttpContext.Items.ContainsKey("TenantId"));

                return Unauthorized(new { error = "UNAUTHORIZED", message = "缺少驗證資訊" });
            }

            var lineUserId = lineUserIdValue.ToString()!;
            var tenantId = tenantIdValue.ToString()!;
            var command = new CorrectDraftItemCommand(
                tenantId, lineUserId, id, request.BuyPrice, request.SellPrice);
            
            var draftItem = await _mediator.Send(command, cancellationToken);
            
            var dto = new DraftItemDto
            {
                Id = draftItem.Id,
                Name = draftItem.Name,
                IsNew = draftItem.IsNew,
                BuyPrice = draftItem.BuyPrice,
                SellPrice = draftItem.SellPrice,
                Quantity = draftItem.Quantity,
                Unit = draftItem.Unit,
                HistoricalAvgPrice = draftItem.HistoricalAvgPrice,
                Validation = new ValidationResultDto
                {
                    Status = draftItem.Validation.Status,
                    Message = draftItem.Validation.Message
                }
            };

            return Ok(dto);
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { error = "NOT_FOUND", message = "Draft session not found" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "NOT_FOUND", message = "Draft item not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "修正草稿品項價格時發生錯誤");
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "系統忙碌中" });
        }
    }

    private static bool IsValidGuidId(string id)
    {
        return !string.IsNullOrEmpty(id) && 
               id.Length == 32 && 
               Regex.IsMatch(id, @"^[a-f0-9]{32}$", RegexOptions.IgnoreCase);
    }

    private static bool IsValidDecimalPlaces(decimal? value)
    {
        if (value is null) return true;
        return decimal.Round(value.Value, 2) == value.Value;
    }
}