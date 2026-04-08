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
    private readonly IWebHostEnvironment _environment;

    public LiffAuthFilter(
        ILineTokenService tokenService,
        IOptions<LineOptions> lineOptions,
        ILogger<LiffAuthFilter> logger,
        IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _lineOptions = lineOptions.Value;
        _logger = logger;
        _environment = environment;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // ── Testing 環境 Mock Auth Bypass ──────────────────────────────────────
        // 此路徑嚴格限定在 Testing 環境，Production/Development 完全無效。
        // 觸發條件：ASPNETCORE_ENVIRONMENT=Testing AND X-Test-Auth: true header
        if (_environment.IsEnvironment("Testing") &&
            context.HttpContext.Request.Headers.TryGetValue("X-Test-Auth", out var testAuthHeader) &&
            testAuthHeader.ToString() == "true")
        {
            var testTenantId = context.HttpContext.Request.Headers["X-Test-TenantId"].ToString();
            var testUserId   = context.HttpContext.Request.Headers["X-Test-LineUserId"].ToString();
            var testDisplay  = context.HttpContext.Request.Headers["X-Test-DisplayName"].ToString();

            context.HttpContext.Items["TenantId"]         = string.IsNullOrEmpty(testTenantId) ? "test-tenant" : testTenantId;
            context.HttpContext.Items["LineUserId"]        = string.IsNullOrEmpty(testUserId)   ? "test-user"   : testUserId;
            context.HttpContext.Items["LineDisplayName"]   = string.IsNullOrEmpty(testDisplay)  ? "Test User"   : testDisplay;

            _logger.LogDebug("Testing mock auth: TenantId={TenantId}, UserId={UserId}",
                context.HttpContext.Items["TenantId"], context.HttpContext.Items["LineUserId"]);

            await next();
            return;
        }
        // ─────────────────────────────────────────────────────────────────────

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