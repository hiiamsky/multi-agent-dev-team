using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Infrastructure.Line;

namespace VeggieAlly.WebAPI.Filters;

/// <summary>
/// LINE LIFF Access Token 驗證 Filter
/// </summary>
public class LiffAuthFilter : IAsyncActionFilter
{
    private readonly ILineTokenService _tokenService;
    private readonly LineOptions _lineOptions;
    private readonly ILogger<LiffAuthFilter> _logger;

    public LiffAuthFilter(ILineTokenService tokenService, IOptions<LineOptions> lineOptions, ILogger<LiffAuthFilter> logger)
    {
        _tokenService = tokenService;
        _lineOptions = lineOptions.Value;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 提取 Authorization header
        if (!context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            authHeader.Count == 0)
        {
            _logger.LogWarning("請求缺少 Authorization header");
            context.Result = new UnauthorizedObjectResult(new { error = "UNAUTHORIZED", message = "Missing authorization header" });
            return;
        }

        var headerValue = authHeader.FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue) || !headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Authorization header 格式錯誤");
            context.Result = new UnauthorizedObjectResult(new { error = "UNAUTHORIZED", message = "Invalid authorization format" });
            return;
        }

        var token = headerValue["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Access token 為空");
            context.Result = new UnauthorizedObjectResult(new { error = "UNAUTHORIZED", message = "Missing access token" });
            return;
        }

        // 驗證 token
        var tokenClaim = await _tokenService.VerifyAccessTokenAsync(token);
        if (tokenClaim is null)
        {
            _logger.LogWarning("LINE Access Token 驗證失敗");
            context.Result = new UnauthorizedObjectResult(new { error = "UNAUTHORIZED", message = "Invalid LINE access token" });
            return;
        }

        // 設置 context items
        context.HttpContext.Items["LineUserId"] = tokenClaim.UserId;
        context.HttpContext.Items["TenantId"] = _lineOptions.TenantId;
        context.HttpContext.Items["LineDisplayName"] = tokenClaim.DisplayName;

        _logger.LogDebug("LIFF 認證成功，UserId: {UserId}", tokenClaim.UserId);
        await next();
    }
}

/// <summary>
/// LIFF 認證屬性
/// </summary>
public class LiffAuthAttribute : ServiceFilterAttribute
{
    public LiffAuthAttribute() : base(typeof(LiffAuthFilter))
    {
    }
}