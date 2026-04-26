using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace VeggieAlly.WebAPI.Configuration;

/// <summary>
/// 不進行任何驗證的 Null Authentication Handler。
/// 用於支援 ASP.NET Core 授權管道（FallbackPolicy），
/// 同時保留現有 ActionFilter 驗證機制不受影響。
/// 所有已保護的 Controller 須標記 [AllowAnonymous]，由自訂 Filter 負責驗證；
/// 未標記的新 Controller 將被 FallbackPolicy 阻擋並返回 401。
/// </summary>
internal sealed class NullAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public NullAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.NoResult());
}
