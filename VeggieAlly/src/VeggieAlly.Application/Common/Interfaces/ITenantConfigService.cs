namespace VeggieAlly.Application.Common.Interfaces;

/// <summary>
/// 租戶配置服務介面
/// </summary>
public interface ITenantConfigService
{
    string GetTenantId();
}