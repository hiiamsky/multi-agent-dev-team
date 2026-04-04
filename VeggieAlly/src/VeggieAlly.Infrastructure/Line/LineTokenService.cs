using System.Text.Json;
using Microsoft.Extensions.Logging;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Infrastructure.Line;

/// <summary>
/// LINE Access Token 驗證服務實作
/// </summary>
public sealed class LineTokenService : ILineTokenService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LineTokenService> _logger;

    public LineTokenService(HttpClient httpClient, ILogger<LineTokenService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LineTokenClaim?> VerifyAccessTokenAsync(string accessToken, CancellationToken ct = default)
    {
        try
        {
            // Step 1: 驗證 token 有效性
            var encodedToken = Uri.EscapeDataString(accessToken);
            var verifyResponse = await _httpClient.GetAsync($"/oauth2/v2.1/verify?access_token={encodedToken}", ct);
            if (!verifyResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("LINE Access Token 驗證失敗，Status: {Status}", verifyResponse.StatusCode);
                return null;
            }

            var verifyJson = await verifyResponse.Content.ReadAsStringAsync(ct);
            var verifyResult = JsonSerializer.Deserialize<TokenVerifyResponse>(verifyJson, JsonOptions);
            
            if (verifyResult?.ExpiresIn <= 0)
            {
                _logger.LogWarning("LINE Access Token 已過期");
                return null;
            }

            // Step 2: 取得使用者資料
            var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/v2/profile");
            profileRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var profileResponse = await _httpClient.SendAsync(profileRequest, ct);
            if (!profileResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("LINE Profile 取得失敗，Status: {Status}", profileResponse.StatusCode);
                return null;
            }

            var profileJson = await profileResponse.Content.ReadAsStringAsync(ct);
            var profile = JsonSerializer.Deserialize<LineProfileResponse>(profileJson, JsonOptions);

            if (string.IsNullOrEmpty(profile?.UserId))
            {
                _logger.LogWarning("LINE Profile 回應格式異常");
                return null;
            }

            return new LineTokenClaim(profile.UserId, profile.DisplayName ?? "Unknown");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "LINE Token 驗證網路錯誤");
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(ex, "LINE Token 驗證超時");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE Token 驗證發生未預期錯誤");
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private sealed record TokenVerifyResponse(string? Scope, string? ClientId, int ExpiresIn);
    private sealed record LineProfileResponse(string? UserId, string? DisplayName);
}