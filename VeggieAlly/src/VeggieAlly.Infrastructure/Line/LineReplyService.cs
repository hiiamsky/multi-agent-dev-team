using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeggieAlly.Domain.Abstractions;

namespace VeggieAlly.Infrastructure.Line;

public sealed class LineReplyService : ILineReplyService
{
    private readonly HttpClient _httpClient;
    private readonly LineOptions _options;
    private readonly ILogger<LineReplyService> _logger;

    public LineReplyService(HttpClient httpClient, IOptions<LineOptions> options, ILogger<LineReplyService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ReplyTextAsync(string replyToken, string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(replyToken))
        {
            _logger.LogWarning("ReplyToken 為空，無法回覆");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("回覆文字內容為空");
            return;
        }

        // LINE API 限制單一訊息長度為 5000 字元
        if (text.Length > 5000)
        {
            text = text.Substring(0, 4997) + "...";
            _logger.LogWarning("回覆文字超過 5000 字元，已截斷");
        }

        try
        {
            var requestPayload = new
            {
                replyToken = replyToken,
                messages = new[]
                {
                    new { type = "text", text = text }
                }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ChannelAccessToken}");

            var response = await _httpClient.PostAsJsonAsync("/v2/bot/message/reply", requestPayload, ct);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("成功回覆 LINE 訊息");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("LINE Reply API 回傳錯誤: {StatusCode}, 內容: {Content}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "呼叫 LINE Reply API 時發生例外");
            throw;
        }
    }
}