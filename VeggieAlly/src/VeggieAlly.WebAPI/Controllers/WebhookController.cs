using MediatR;
using Microsoft.AspNetCore.Mvc;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.LineEvents.ProcessAudio;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Application.Menu.Publish;
using VeggieAlly.Domain.Models.Line;
using VeggieAlly.Domain.Exceptions;
using VeggieAlly.WebAPI.Filters;

namespace VeggieAlly.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantConfigService _tenantConfigService;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(IMediator mediator, ITenantConfigService tenantConfigService, ILogger<WebhookController> logger)
    {
        _mediator = mediator;
        _tenantConfigService = tenantConfigService;
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
            // 跳過沒有 ReplyToken 的事件（如 Webhook 驗證事件）
            if (string.IsNullOrWhiteSpace(lineEvent.ReplyToken))
            {
                _logger.LogDebug("跳過沒有 ReplyToken 的事件");
                continue;
            }

            try
            {
                switch (lineEvent.Type)
                {
                    case "message":
                        await HandleMessageEvent(lineEvent);
                        break;
                    case "postback":
                        await HandlePostbackEvent(lineEvent);
                        break;
                    default:
                        _logger.LogDebug("跳過不支援的事件類型: Type={EventType}", lineEvent.Type);
                        break;
                }
            }
            catch (Exception ex)
            {
                // 不向 LINE Platform 回傳錯誤，避免重複投遞
                _logger.LogError(ex, "處理事件時發生例外，但不影響回傳狀態: EventType={EventType}", lineEvent.Type);
            }
        }

        // 永遠回傳 200 OK
        return Ok();
    }

    private async Task HandleMessageEvent(LineEvent lineEvent)
    {
        switch (lineEvent.Message?.Type)
        {
            case "text":
                await _mediator.Send(new ProcessTextMessageCommand(lineEvent));
                _logger.LogInformation("成功處理文字訊息事件");
                break;
            case "audio":
                await _mediator.Send(new ProcessAudioMessageCommand(lineEvent));
                _logger.LogInformation("成功處理語音訊息事件");
                break;
            default:
                _logger.LogDebug("跳過不支援的訊息類型: {MessageType}", lineEvent.Message?.Type);
                break;
        }
    }

    private async Task HandlePostbackEvent(LineEvent lineEvent)
    {
        if (lineEvent.Postback?.Data is null)
        {
            _logger.LogDebug("Postback 事件沒有 data 欄位");
            return;
        }

        try
        {
            // 解析 Postback data: "action=publish&tenant_id=X&user_id=Y"
            var postbackData = ParsePostbackData(lineEvent.Postback.Data);
            
            if (!postbackData.TryGetValue("action", out var action))
            {
                _logger.LogDebug("Postback data 缺少 action 參數: {Data}", lineEvent.Postback.Data);
                return;
            }

            switch (action)
            {
                case "publish":
                    await HandlePublishPostback(lineEvent);
                    _logger.LogInformation("成功處理 publish postback 事件");
                    break;
                default:
                    _logger.LogDebug("跳過不支援的 postback action: {Action}", action);
                    break;
            }
        }
        catch (MenuAlreadyPublishedException)
        {
            _logger.LogWarning("嘗試重複發布菜單: UserId={UserId}", 
                lineEvent.Source.UserId);
            // 這裡可以選擇不回覆或回覆已發布訊息
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理 Postback 事件時發生例外: Data={Data}", lineEvent.Postback.Data);
        }
    }

    private async Task HandlePublishPostback(LineEvent lineEvent)
    {
        var tenantId = _tenantConfigService.GetTenantId();
        var userId = lineEvent.Source.UserId;

        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Postback 事件缺少 user 資訊");
            return;
        }

        var command = new PublishMenuCommand(tenantId, userId);
        await _mediator.Send(command);
    }

    private static Dictionary<string, string> ParsePostbackData(string data)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var pair in data.Split('&'))
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length == 2)
            {
                result[keyValue[0]] = Uri.UnescapeDataString(keyValue[1]);
            }
        }

        return result;
    }
}