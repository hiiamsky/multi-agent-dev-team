using MediatR;
using Microsoft.AspNetCore.Mvc;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Domain.Models.Line;
using VeggieAlly.WebAPI.Filters;

namespace VeggieAlly.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IMediator mediator, ILogger<WebhookController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    [TypeFilter(typeof(LineSignatureAuthFilter))]
    public async Task<IActionResult> Receive([FromBody] LineWebhookPayload payload)
    {
        if (payload?.Events == null || !payload.Events.Any())
        {
            _logger.LogInformation("收到空的 Events 陣列");
            return Ok();
        }

        foreach (var lineEvent in payload.Events)
        {
            // 過濾條件：僅處理 message 類型的 text 事件
            if (lineEvent.Type != "message" || lineEvent.Message?.Type != "text")
            {
                _logger.LogDebug("跳過非文字訊息事件: Type={EventType}, MessageType={MessageType}", 
                    lineEvent.Type, lineEvent.Message?.Type);
                continue;
            }

            // 跳過沒有 ReplyToken 的事件（如 Webhook 驗證事件）
            if (string.IsNullOrWhiteSpace(lineEvent.ReplyToken))
            {
                _logger.LogDebug("跳過沒有 ReplyToken 的事件");
                continue;
            }

            try
            {
                var command = new ProcessTextMessageCommand(lineEvent);
                await _mediator.Send(command);
                _logger.LogInformation("成功處理文字訊息事件");
            }
            catch (Exception ex)
            {
                // 不向 LINE Platform 回傳錯誤，避免重複投遞
                _logger.LogError(ex, "處理訊息事件時發生例外，但不影響回傳狀態");
            }
        }

        // 永遠回傳 200 OK
        return Ok();
    }
}