using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Application.Tests;

public sealed class ProcessTextMessageHandlerTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILineReplyService _lineReplyService = Substitute.For<ILineReplyService>();
    private readonly IValidationReplyService _validationReplyService = Substitute.For<IValidationReplyService>();
    private readonly ITenantConfigService _tenantConfigService = Substitute.For<ITenantConfigService>();
    private readonly ILogger<ProcessTextMessageHandler> _logger = Substitute.For<ILogger<ProcessTextMessageHandler>>();
    private readonly ProcessTextMessageHandler _handler;

    public ProcessTextMessageHandlerTests()
    {
        _tenantConfigService.GetTenantId().Returns("default");
        _handler = new ProcessTextMessageHandler(_chatClient, _lineReplyService, _validationReplyService, _tenantConfigService, _logger);
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
    public async Task Handle_ValidText_CallsValidationReplyService()
    {
        SetupChatClientReturns("{\"items\":[]}");
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _validationReplyService.Received(1).ProcessLlmResponseAndReplyAsync(
            "{\"items\":[]}",
            "reply-token-123",
            "default",
            "U123",
            default);
    }

    [Fact]
    public async Task Handle_EmptyText_DoesNotCallChatClient()
    {
        var command = CreateCommand("");

        await _handler.Handle(command, default);

        await _chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyReplyToken_DoesNotCallAnything()
    {
        var command = CreateCommand(replyToken: "");

        await _handler.Handle(command, default);

        await _chatClient.DidNotReceive().GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
        await _validationReplyService.DidNotReceive().ProcessLlmResponseAndReplyAsync(
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ChatClientThrows_RepliesBusyMessage()
    {
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API error"));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "系統忙碌中，請稍後重試",
            default);
    }

    [Fact]
    public async Task Handle_ChatClientReturnsNull_DelegatesEmptyToService()
    {
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, (string?)null)));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _validationReplyService.Received(1).ProcessLlmResponseAndReplyAsync(
            "",
            "reply-token-123",
            "default",
            "U123",
            default);
    }

    [Fact]
    public async Task Handle_BusyReplyAlsoThrows_DoesNotThrow()
    {
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("API error"));
        _lineReplyService.ReplyTextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Reply also failed"));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        // Should complete without throwing
    }
}