using System.Text.Json;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Prompts;
using VeggieAlly.Domain.Abstractions;

namespace VeggieAlly.Application.LineEvents.ProcessText;

public sealed class ProcessTextMessageHandler : IRequestHandler<ProcessTextMessageCommand>
{
    private readonly IChatClient _chatClient;
    private readonly ILineReplyService _lineReplyService;
    private readonly ILogger<ProcessTextMessageHandler> _logger;

    public ProcessTextMessageHandler(
        IChatClient chatClient,
        ILineReplyService lineReplyService,
        ILogger<ProcessTextMessageHandler> logger)
    {
        _chatClient = chatClient;
        _lineReplyService = lineReplyService;
        _logger = logger;
    }

    public async Task Handle(ProcessTextMessageCommand request, CancellationToken cancellationToken)
    {
        var textMessage = request.Event.Message?.Text;
        if (string.IsNullOrWhiteSpace(textMessage))
        {
            _logger.LogWarning("收到空的文字訊息，跳過處理");
            return;
        }

        var replyToken = request.Event.ReplyToken;
        if (string.IsNullOrWhiteSpace(replyToken))
        {
            _logger.LogWarning("ReplyToken 為空，無法回覆");
            return;
        }

        // ── Step 1: 呼叫 Gemini API ──
        string replyText;
        try
        {
            var messages = new ChatMessage[]
            {
                new(ChatRole.System, SystemPrompts.VegetableParser),
                new(ChatRole.User, textMessage)
            };

            var completion = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseContent = completion?.Text;

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                _logger.LogWarning("Gemini 回傳空內容");
                replyText = "解析失敗，請重新輸入";
            }
            else if (!IsValidJson(responseContent))
            {
                _logger.LogWarning("Gemini 回傳非 JSON 格式: {Content}", responseContent);
                replyText = "解析失敗，請重新輸入";
            }
            else
            {
                replyText = responseContent;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API 呼叫失敗");
            replyText = "系統忙碌中，請稍後重試";
        }

        // ── Step 2: 回覆 LINE 使用者（獨立 try-catch） ──
        try
        {
            await _lineReplyService.ReplyTextAsync(replyToken, replyText, cancellationToken);
            _logger.LogInformation("成功處理文字訊息並回覆");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LINE Reply 失敗，無法回覆使用者");
        }
    }

    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}