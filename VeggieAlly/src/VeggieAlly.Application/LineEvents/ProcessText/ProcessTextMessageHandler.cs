using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Prompts;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Parsing;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.LineEvents.ProcessText;

public sealed class ProcessTextMessageHandler : IRequestHandler<ProcessTextMessageCommand>
{
    private readonly IChatClient _chatClient;
    private readonly ILineReplyService _lineReplyService;
    private readonly IVegetablePricingService _pricingService;
    private readonly IPriceValidationService _validationService;
    private readonly ILogger<ProcessTextMessageHandler> _logger;

    public ProcessTextMessageHandler(
        IChatClient chatClient,
        ILineReplyService lineReplyService,
        IVegetablePricingService pricingService,
        IPriceValidationService validationService,
        ILogger<ProcessTextMessageHandler> logger)
    {
        _chatClient = chatClient;
        _lineReplyService = lineReplyService;
        _pricingService = pricingService;
        _validationService = validationService;
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
            var responseContent = completion?.Text?.Trim();

            // LLM 有時會用 markdown 代碼框包裹 JSON，去除之
            responseContent = StripMarkdownCodeFence(responseContent);

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
                // ── Step 1.5: 解析 JSON 並進行價格驗證 ──
                replyText = await ProcessValidationAsync(responseContent, cancellationToken);
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

    private async Task<string> ProcessValidationAsync(string jsonContent, CancellationToken cancellationToken)
    {
        try
        {
            // 反序列化為物件（items 陣列）
            var parseResult = JsonSerializer.Deserialize<ParseResult>(jsonContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (parseResult?.Items == null || parseResult.Items.Count == 0)
            {
                _logger.LogWarning("JSON 反序列化成功但無有效品項");
                return "解析失敗，請重新輸入";
            }

            // 走訪每個 item，查歷史價 + 驗證
            var validatedItems = new List<ValidatedVegetableItem>();
            
            foreach (var item in parseResult.Items)
            {
                // 查詢歷史價格
                var historicalPrice = await _pricingService.GetHistoricalAvgPriceAsync(item.Name, cancellationToken: cancellationToken);
                
                // 執行價格驗證
                var validation = _validationService.Validate(item.BuyPrice, item.SellPrice, historicalPrice);
                
                // 組合為 ValidatedVegetableItem
                var validatedItem = new ValidatedVegetableItem(
                    item.Name,
                    item.IsNew,
                    item.BuyPrice,
                    item.SellPrice,
                    item.Quantity,
                    item.Unit,
                    historicalPrice,
                    validation
                );
                
                validatedItems.Add(validatedItem);
            }

            // 產生回覆文字：用 🟢/🔴 標示每個品項的驗證結果
            return GenerateValidationReply(validatedItems);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON 反序列化失敗: {Json}", jsonContent);
            return "解析失敗，請重新輸入";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "價格驗證過程發生錯誤");
            return "系統忙碌中，請稍後重試";
        }
    }

    private static string GenerateValidationReply(List<ValidatedVegetableItem> validatedItems)
    {
        var reply = new StringBuilder("📋 報價驗證結果：\n");

        foreach (var item in validatedItems)
        {
            var icon = item.Validation.Status == ValidationStatus.Ok ? "🟢" : "🔴";
            var warning = item.Validation.Status != ValidationStatus.Ok && !string.IsNullOrEmpty(item.Validation.Message)
                ? $" ⚠️ {item.Validation.Message}"
                : "";

            reply.AppendLine($"{icon} {item.Name}｜進${item.BuyPrice} 售${item.SellPrice} x{item.Quantity}{item.Unit}{warning}");
        }

        return reply.ToString();
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

    private static string? StripMarkdownCodeFence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            // 去除第一行 ```json 或 ```
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
                trimmed = trimmed[(firstNewLine + 1)..];

            // 去除最後的 ```
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3].TrimEnd();
        }

        return trimmed;
    }
}