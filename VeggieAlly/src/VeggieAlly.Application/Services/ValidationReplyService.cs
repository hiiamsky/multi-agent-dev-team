using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Parsing;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Services;

public sealed class ValidationReplyService : IValidationReplyService
{
    private readonly ILineReplyService _lineReplyService;
    private readonly IVegetablePricingService _pricingService;
    private readonly IPriceValidationService _validationService;
    private readonly IFlexMessageBuilder _flexMessageBuilder;
    private readonly IDraftMenuService _draftMenuService;
    private readonly ILiffConfigService _liffConfigService;
    private readonly ILogger<ValidationReplyService> _logger;

    public ValidationReplyService(
        ILineReplyService lineReplyService,
        IVegetablePricingService pricingService,
        IPriceValidationService validationService,
        IFlexMessageBuilder flexMessageBuilder,
        IDraftMenuService draftMenuService,
        ILiffConfigService liffConfigService,
        ILogger<ValidationReplyService> logger)
    {
        _lineReplyService = lineReplyService;
        _pricingService = pricingService;
        _validationService = validationService;
        _flexMessageBuilder = flexMessageBuilder;
        _draftMenuService = draftMenuService;
        _liffConfigService = liffConfigService;
        _logger = logger;
    }

    public async Task ProcessLlmResponseAndReplyAsync(
        string? llmResponse, string replyToken,
        string tenantId, string lineUserId,
        CancellationToken ct = default)
    {
        var responseContent = StripMarkdownCodeFence(llmResponse);

        List<ValidatedVegetableItem>? validatedItems = null;
        string? fallbackText = null;

        if (string.IsNullOrWhiteSpace(responseContent))
        {
            _logger.LogWarning("LLM 回傳空內容");
            fallbackText = "解析失敗，請重新輸入";
        }
        else if (!IsValidJson(responseContent))
        {
            var truncatedContent = responseContent?.Length > 200
                ? responseContent[..200] + "...[truncated]"
                : responseContent;
            _logger.LogWarning("LLM 回傳非 JSON 格式 (長度={Length}): {Content}",
                responseContent?.Length, truncatedContent);
            fallbackText = "解析失敗，請重新輸入";
        }
        else
        {
            validatedItems = await ProcessValidationAsync(responseContent, ct);
            if (validatedItems is null)
                fallbackText = "解析失敗，請重新輸入";
        }

        try
        {
            if (validatedItems is not null)
            {
                try
                {
                    // 先嘗試建立或合併草稿
                    object bubble;
                    string altText;
                    
                    try
                    {
                        var session = await _draftMenuService.CreateOrMergeDraftAsync(
                            tenantId, lineUserId, validatedItems, ct);
                        
                        var liffBaseUrl = _liffConfigService.GetLiffBaseUrl();
                        bubble = _flexMessageBuilder.BuildDraftBubble(session, liffBaseUrl);
                        
                        var okCount = session.Items.Count(i => i.Validation.Status == ValidationStatus.Ok);
                        var anomalyCount = session.Items.Count - okCount;
                        altText = $"📋 報價驗證結果：{okCount}項正常, {anomalyCount}項異常 (可修正)";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "草稿儲存失敗，降級為無修正按鈕模式");
                        // 降級為傳統模式
                        bubble = _flexMessageBuilder.BuildBubble(validatedItems);
                        var okCount = validatedItems.Count(i => i.Validation.Status == ValidationStatus.Ok);
                        var anomalyCount = validatedItems.Count - okCount;
                        altText = $"📋 報價驗證結果：{okCount}項正常, {anomalyCount}項異常";
                    }

                    await _lineReplyService.ReplyFlexAsync(replyToken, altText, bubble, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Flex Message 建構失敗，降級為純文字回覆");
                    await _lineReplyService.ReplyTextAsync(replyToken, GenerateValidationReply(validatedItems), ct);
                }
            }
            else
            {
                await _lineReplyService.ReplyTextAsync(replyToken, fallbackText!, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LINE Reply 主要流程失敗，嘗試 fallback");
            
            // 嘗試 fallback 處理
            try
            {
                var fallbackMessage = validatedItems is not null 
                    ? GenerateValidationReply(validatedItems)
                    : fallbackText ?? "系統忙碌中，請稍後重試";
                    
                // TODO: 在此處實作 LINE Push Message API 作為 fallback
                // await _linePushService.PushMessageAsync(lineUserId, fallbackMessage, ct);
                
                _logger.LogWarning("LINE Reply 失敗但草稿已建立，用戶可能未收到回覆。LineUserId: {LineUserId}, TenantId: {TenantId}", 
                    lineUserId ?? "unknown", tenantId ?? "unknown");
            }
            catch (Exception fallbackEx)
            {
                _logger.LogWarning(fallbackEx, "LINE Reply fallback 也失敗，用戶未收到任何回覆。LineUserId: {LineUserId}, TenantId: {TenantId}", 
                    lineUserId ?? "unknown", tenantId ?? "unknown");
            }
        }
    }

    private async Task<List<ValidatedVegetableItem>?> ProcessValidationAsync(string jsonContent, CancellationToken ct)
    {
        try
        {
            var parseResult = JsonSerializer.Deserialize<ParseResult>(jsonContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (parseResult?.Items == null || parseResult.Items.Count == 0)
            {
                _logger.LogWarning("JSON 反序列化成功但無有效品項");
                return null;
            }

            var validatedItems = new List<ValidatedVegetableItem>();

            foreach (var item in parseResult.Items)
            {
                var historicalPrice = await _pricingService.GetHistoricalAvgPriceAsync(item.Name, cancellationToken: ct);
                var validation = _validationService.Validate(item.BuyPrice, item.SellPrice, historicalPrice);
                var validatedItem = new ValidatedVegetableItem(
                    item.Name, item.IsNew, item.BuyPrice, item.SellPrice,
                    item.Quantity, item.Unit, historicalPrice, validation);
                validatedItems.Add(validatedItem);
            }

            return validatedItems;
        }
        catch (JsonException ex)
        {
            var truncated = jsonContent?.Length > 200
                ? jsonContent[..200] + "...[truncated]"
                : jsonContent;
            _logger.LogError(ex, "JSON 反序列化失敗 (長度={Length}): {Json}", jsonContent?.Length, truncated);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "價格驗證過程發生錯誤");
            return null;
        }
    }

    internal static string GenerateValidationReply(List<ValidatedVegetableItem> validatedItems)
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

    internal static string? StripMarkdownCodeFence(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
                trimmed = trimmed[(firstNewLine + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3].TrimEnd();
        }

        return trimmed;
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
