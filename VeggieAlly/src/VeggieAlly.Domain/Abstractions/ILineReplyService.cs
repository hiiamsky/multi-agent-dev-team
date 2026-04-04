namespace VeggieAlly.Domain.Abstractions;

public interface ILineReplyService
{
    Task ReplyTextAsync(string replyToken, string text, CancellationToken ct = default);
}