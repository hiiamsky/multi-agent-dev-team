using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Prompts;
using VeggieAlly.Domain.Abstractions;

namespace VeggieAlly.Application.LineEvents.ProcessText;

public sealed class ProcessTextMessageHandler : IRequestHandler<ProcessTextMessageCommand>
{
    private readonly IChatClient _chatClient;
    private readonly ILineReplyService _lineReplyService;
    private readonly IValidationReplyService _validationReplyService;
    private readonly ILogger<ProcessTextMessageHandler> _logger;

    public ProcessTextMessageHandler(
        IChatClient chatClient,
        ILineReplyService lineReplyService,
        IValidationReplyService validationReplyService,
        ILogger<ProcessTextMessageHandler> logger)
    {
        _chatClient = chatClient;
        _lineReplyService = lineReplyService;
        _validationReplyService = validationReplyService;
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
            var messages = new ChatMessage[]
            {
                new(ChatRole.System, SystemPrompts.VegetableParser),
                new(ChatRole.User, textMessage)
            };

            var completion = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var llmResponse = completion?.Text?.Trim();

            await _validationReplyService.ProcessLlmResponseAndReplyAsync(llmResponse, replyToken, cancellationToken);
            _logger.LogInformation("成功處理文字訊息並回覆");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API 呼叫失敗");
            try
            {
                await _lineReplyService.ReplyTextAsync(replyToken, "系統忙碌中，請稍後重試", cancellationToken);
            }
            catch (Exception replyEx)
            {
                _logger.LogWarning(replyEx, "LINE Reply 失敗，無法回覆使用者");
            }
        }
    }
}