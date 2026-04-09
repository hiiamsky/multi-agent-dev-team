using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using VeggieAlly.Infrastructure.Line;
using VeggieAlly.WebAPI.Configuration;

namespace VeggieAlly.WebAPI.Filters;

public sealed class LineSignatureAuthFilter : IAsyncResourceFilter
{
    private readonly LineSettings _lineSettings;
    private readonly ILogger<LineSignatureAuthFilter> _logger;

    public LineSignatureAuthFilter(IOptions<LineSettings> lineSettings, ILogger<LineSignatureAuthFilter> logger)
    {
        _lineSettings = lineSettings.Value;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;

        // 啟用 Request Body 的重複讀取（在 model binding 之前）
        request.EnableBuffering();

        // 取得 X-Line-Signature Header
        if (!request.Headers.TryGetValue("X-Line-Signature", out var signatures) || signatures.Count == 0)
        {
            _logger.LogWarning("X-Line-Signature Header 缺失");
            context.Result = new UnauthorizedResult();
            return;
        }

        var signature = signatures.First();
        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("X-Line-Signature Header 為空");
            context.Result = new UnauthorizedResult();
            return;
        }

        // 讀取 Request Body
        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);
        var requestBody = memoryStream.ToArray();

        // 重設 Body 位置以供 model binding 讀取
        request.Body.Position = 0;

        // 驗證簽章（使用安全注入的設定）
        var isValid = LineSignatureValidator.Validate(_lineSettings.ChannelSecret, requestBody, signature);
        
        if (!isValid)
        {
            _logger.LogWarning("LINE Signature 驗證失敗");
            context.Result = new UnauthorizedResult();
            return;
        }

        _logger.LogDebug("LINE Signature 驗證通過");
        await next();
    }
}