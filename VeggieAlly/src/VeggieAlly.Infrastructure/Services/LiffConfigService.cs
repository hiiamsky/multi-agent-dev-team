using Microsoft.Extensions.Options;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Infrastructure.Line;

namespace VeggieAlly.Infrastructure.Services;

/// <summary>
/// LIFF 配置服務實作
/// </summary>
public sealed class LiffConfigService : ILiffConfigService
{
    private readonly LineOptions _lineOptions;

    public LiffConfigService(IOptions<LineOptions> lineOptions)
    {
        _lineOptions = lineOptions.Value;
    }

    public string? GetLiffBaseUrl() => _lineOptions.LiffBaseUrl;
}