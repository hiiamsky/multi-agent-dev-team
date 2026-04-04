using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VeggieAlly.Domain.Abstractions;

namespace VeggieAlly.Infrastructure.Line;

public sealed class LineContentService : ILineContentService
{
    private readonly HttpClient _httpClient;
    private readonly LineOptions _options;
    private readonly ILogger<LineContentService> _logger;

    public LineContentService(HttpClient httpClient, IOptions<LineOptions> options, ILogger<LineContentService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<byte[]> DownloadContentAsync(string messageId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/v2/bot/message/{messageId}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("成功下載語音內容: MessageId={MessageId}", messageId);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
