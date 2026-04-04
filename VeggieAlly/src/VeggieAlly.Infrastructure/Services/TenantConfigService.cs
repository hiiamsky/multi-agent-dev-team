using Microsoft.Extensions.Options;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Infrastructure.Line;

namespace VeggieAlly.Infrastructure.Services;

/// <summary>
/// 租戶配置服務實作
/// </summary>
public sealed class TenantConfigService : ITenantConfigService
{
    private readonly LineOptions _lineOptions;

    public TenantConfigService(IOptions<LineOptions> lineOptions)
    {
        _lineOptions = lineOptions.Value;
    }

    public string GetTenantId() => _lineOptions.TenantId;
}