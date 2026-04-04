namespace VeggieAlly.Application.Common.Interfaces;

public interface IValidationReplyService
{
    Task ProcessLlmResponseAndReplyAsync(
        string? llmResponse, string replyToken,
        string tenantId, string lineUserId,
        CancellationToken ct = default);
}
