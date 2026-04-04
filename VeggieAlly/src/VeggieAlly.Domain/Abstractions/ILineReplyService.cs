namespace VeggieAlly.Domain.Abstractions;

public interface ILineReplyService
{
    Task ReplyTextAsync(string replyToken, string text, CancellationToken ct = default);
    Task ReplyFlexAsync(string replyToken, string altText, object flexContent, CancellationToken ct = default);
}