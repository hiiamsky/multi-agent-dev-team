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

        try
        {
            // 組裝 ChatMessage 陣列：SystemPrompt + 使用者文字
            var messages = new ChatMessage[]
            {
                new(ChatRole.System, SystemPrompts.VegetableParser),
                new(ChatRole.User, textMessage)
            };

            // 呼叫 Gemini API 
            var completion = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var responseContent = completion?.Text;

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                await _lineReplyService.ReplyTextAsync(replyToken, "解析失敗，請重新輸入", cancellationToken);
                _logger.LogWarning("Gemini 回傳空內容");
                return;
            }

            // 將 Gemini 回傳的 JSON 直接回傳給使用者
            await _lineReplyService.ReplyTextAsync(replyToken, responseContent, cancellationToken);
            _logger.LogInformation("成功處理文字訊息並回覆");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "處理文字訊息時發生例外");
            
            // 內部 try-catch：嘗試回傳錯誤訊息給使用者
            try
            {
                await _lineReplyService.ReplyTextAsync(replyToken, "系統忙碌中，請稍後重試", cancellationToken);
            }
            catch (Exception replyEx)
            {
                _logger.LogWarning(replyEx, "回傳錯誤訊息失敗，無法回覆使用者");
            }
        }
    }
}