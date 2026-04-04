using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Prompts;
using VeggieAlly.Domain.Abstractions;

namespace VeggieAlly.Application.LineEvents.ProcessAudio;

public sealed class ProcessAudioMessageHandler : IRequestHandler<ProcessAudioMessageCommand>
{
    private readonly IChatClient _chatClient;
    private readonly ILineReplyService _lineReplyService;
    private readonly ILineContentService _lineContentService;
    private readonly IValidationReplyService _validationReplyService;
    private readonly ITenantConfigService _tenantConfigService;
    private readonly ILogger<ProcessAudioMessageHandler> _logger;

    public ProcessAudioMessageHandler(
        IChatClient chatClient,
        ILineReplyService lineReplyService,
        ILineContentService lineContentService,
        IValidationReplyService validationReplyService,
        ITenantConfigService tenantConfigService,
        ILogger<ProcessAudioMessageHandler> logger)
    {
        _chatClient = chatClient;
        _lineReplyService = lineReplyService;
        _lineContentService = lineContentService;
        _validationReplyService = validationReplyService;
        _tenantConfigService = tenantConfigService;
        _logger = logger;
    }

    public async Task Handle(ProcessAudioMessageCommand request, CancellationToken cancellationToken)
    {
        var messageId = request.Event.Message?.Id;
        var replyToken = request.Event.ReplyToken;

        if (string.IsNullOrWhiteSpace(replyToken))
        {
            _logger.LogWarning("ReplyToken 為空，無法回覆");
            return;
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogWarning("語音訊息 ID 為空");
            await SafeReplyAsync(replyToken, "語音下載失敗，請重新錄音", cancellationToken);
            return;
        }

        // ── Step 1: 下載語音檔 ──
        byte[] audioBytes;
        try
        {
            audioBytes = await _lineContentService.DownloadContentAsync(messageId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE Content API 下載語音失敗: MessageId={MessageId}", messageId);
            await SafeReplyAsync(replyToken, "語音下載失敗，請重新錄音", cancellationToken);
            return;
        }

        if (audioBytes.Length == 0)
        {
            _logger.LogWarning("下載的語音內容為空: MessageId={MessageId}", messageId);
            await SafeReplyAsync(replyToken, "語音內容為空，請重新錄音", cancellationToken);
            return;
        }

        // ── Step 2: Gemini 多模態 STT + 結構化解析 ──
        try
        {
            var audioContent = new DataContent(audioBytes, "audio/m4a");
            var messages = new ChatMessage[]
            {
                new(ChatRole.System, SystemPrompts.VegetableParser),
                new(ChatRole.User, new List<AIContent> { audioContent })
            };

            var completion = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var llmResponse = completion?.Text?.Trim();

            var tenantId = _tenantConfigService.GetTenantId();
            var lineUserId = request.Event.Source?.UserId;
            
            if (string.IsNullOrEmpty(lineUserId))
            {
                _logger.LogWarning("LINE UserId 為空，無法處理");
                return;
            }

            await _validationReplyService.ProcessLlmResponseAndReplyAsync(
                llmResponse, replyToken, tenantId, lineUserId, cancellationToken);
            _logger.LogInformation("成功處理語音訊息並回覆");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API 呼叫失敗");
            await SafeReplyAsync(replyToken, "系統忙碌中，請稍後重試", cancellationToken);
        }
    }

    private async Task SafeReplyAsync(string replyToken, string text, CancellationToken ct)
    {
        try
        {
            await _lineReplyService.ReplyTextAsync(replyToken, text, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LINE Reply 失敗，無法回覆使用者");
        }
    }
}
