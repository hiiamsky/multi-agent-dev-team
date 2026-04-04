namespace VeggieAlly.Domain.Abstractions;

public interface ILineContentService
{
    Task<byte[]> DownloadContentAsync(string messageId, CancellationToken ct = default);
}
