namespace VeggieAlly.Domain.Models.Line;

/// <summary>
/// LINE Access Token 驗證後的聲明信息
/// </summary>
public sealed record LineTokenClaim(string UserId, string DisplayName);