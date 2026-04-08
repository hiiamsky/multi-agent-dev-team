using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VeggieAlly.Infrastructure.Storage;

/// <summary>
/// InMemory Draft Session 定期清除服務
/// </summary>
public sealed class InMemoryDraftSessionCleanupService : BackgroundService
{
    private readonly InMemoryDraftSessionStore _store;
    private readonly ILogger<InMemoryDraftSessionCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public InMemoryDraftSessionCleanupService(
        InMemoryDraftSessionStore store,
        ILogger<InMemoryDraftSessionCleanupService> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InMemory Draft Session 清除服務已啟動");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                
                var removedCount = _store.RemoveExpiredEntries();
                if (removedCount > 0)
                {
                    _logger.LogDebug("已清除 {RemovedCount} 個過期 Draft Session", removedCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InMemory Draft Session 清除服務發生錯誤");
        }
        finally
        {
            _logger.LogInformation("InMemory Draft Session 清除服務已停止");
        }
    }
}