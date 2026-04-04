using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// LINE Token 驗證服務介面
/// </summary>
public interface ILineTokenService
{
    Task<LineTokenClaim?> VerifyAccessTokenAsync(string accessToken, CancellationToken ct = default);
}