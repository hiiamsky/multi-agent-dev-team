namespace VeggieAlly.Application.Common.Interfaces;

public interface IValidationReplyService
{
    Task ProcessLlmResponseAndReplyAsync(string? llmResponse, string replyToken, CancellationToken ct = default);
}
