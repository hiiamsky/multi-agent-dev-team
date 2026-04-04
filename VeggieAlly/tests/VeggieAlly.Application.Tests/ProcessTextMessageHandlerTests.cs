using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Application.Tests;

public sealed class ProcessTextMessageHandlerTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILineReplyService _lineReplyService = Substitute.For<ILineReplyService>();
    private readonly ILogger<ProcessTextMessageHandler> _logger = Substitute.For<ILogger<ProcessTextMessageHandler>>();
    private readonly ProcessTextMessageHandler _handler;

    private const string ValidJson = """{"items":[{"name":"初秋高麗菜","is_new":false,"buy_price":25,"sell_price":35,"quantity":50,"unit":"箱"}]}""";

    public ProcessTextMessageHandlerTests()
    {
        _handler = new ProcessTextMessageHandler(_chatClient, _lineReplyService, _logger);
    }

    private static ProcessTextMessageCommand CreateCommand(
        string? text = "高麗菜 25 賣 35 五十箱",
        string? replyToken = "reply-token-123")
    {
        var lineEvent = new LineEvent(
            Type: "message",
            ReplyToken: replyToken,
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("msg1", "text", text));
        return new ProcessTextMessageCommand(lineEvent);
    }

    private void SetupChatClientReturns(string content)
    {
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, content)));
    }

    [Fact]
    public async Task Handle_ValidText_CallsGeminiAndRepliesJson()
    {
        SetupChatClientReturns(ValidJson);
        var command = CreateCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            ValidJson,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GeminiReturnsNonJson_RepliesErrorMessage()
    {
        SetupChatClientReturns("I don't understand");
        var command = CreateCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "解析失敗，請重新輸入",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GeminiReturnsEmpty_RepliesErrorMessage()
    {
        SetupChatClientReturns("");
        var command = CreateCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "解析失敗，請重新輸入",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GeminiThrowsException_RepliesSystemBusy()
    {
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Gemini API 無回應"));
        var command = CreateCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "系統忙碌中，請稍後重試",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyText_SkipsProcessing()
    {
        var command = CreateCommand(text: "");

        await _handler.Handle(command, CancellationToken.None);

        await _chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
        await _lineReplyService.DidNotReceive().ReplyTextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullReplyToken_SkipsProcessing()
    {
        var command = CreateCommand(replyToken: null);

        await _handler.Handle(command, CancellationToken.None);

        await _chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LineReplyFails_DoesNotThrow()
    {
        SetupChatClientReturns(ValidJson);
        _lineReplyService.ReplyTextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("LINE API 無回應"));
        var command = CreateCommand();

        var exception = await Record.ExceptionAsync(() =>
            _handler.Handle(command, CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Handle_GeminiReturnsMarkdownWrappedJson_StripsAndRepliesJson()
    {
        var wrappedJson = "```json\n" + ValidJson + "\n```";
        SetupChatClientReturns(wrappedJson);
        var command = CreateCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            ValidJson,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GeminiReturnsCodeFenceWithoutLang_StripsAndRepliesJson()
    {
        var wrappedJson = "```\n" + ValidJson + "\n```";
        SetupChatClientReturns(wrappedJson);
        var command = CreateCommand();

        await _handler.Handle(command, CancellationToken.None);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            ValidJson,
            Arg.Any<CancellationToken>());
    }
}
