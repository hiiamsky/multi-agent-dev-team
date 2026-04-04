using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.LineEvents.ProcessAudio;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Line;

namespace VeggieAlly.Application.Tests;

public sealed class ProcessAudioMessageHandlerTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILineReplyService _lineReplyService = Substitute.For<ILineReplyService>();
    private readonly ILineContentService _lineContentService = Substitute.For<ILineContentService>();
    private readonly IValidationReplyService _validationReplyService = Substitute.For<IValidationReplyService>();
    private readonly ITenantConfigService _tenantConfigService = Substitute.For<ITenantConfigService>();
    private readonly ILogger<ProcessAudioMessageHandler> _logger = Substitute.For<ILogger<ProcessAudioMessageHandler>>();
    private readonly ProcessAudioMessageHandler _handler;

    public ProcessAudioMessageHandlerTests()
    {
        _tenantConfigService.GetTenantId().Returns("default");
        _handler = new ProcessAudioMessageHandler(_chatClient, _lineReplyService, _lineContentService, _validationReplyService, _tenantConfigService, _logger);
    }

    private static ProcessAudioMessageCommand CreateCommand(
        string? messageId = "audio-msg-001",
        string? replyToken = "reply-token-123")
    {
        var lineEvent = new LineEvent(
            Type: "message",
            ReplyToken: replyToken,
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage(messageId!, "audio", null));
        return new ProcessAudioMessageCommand(lineEvent);
    }

    private void SetupAudioDownload(byte[]? audioBytes = null)
    {
        _lineContentService.DownloadContentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(audioBytes ?? new byte[] { 0x01, 0x02, 0x03 });
    }

    private void SetupLlmReturns(string content)
    {
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, content)));
    }

    [Fact]
    public async Task Handle_ValidAudio_DownloadsAndCallsValidationService()
    {
        SetupAudioDownload();
        SetupLlmReturns("{\"items\":[]}");
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineContentService.Received(1).DownloadContentAsync("audio-msg-001", default);
        await _chatClient.Received(1).GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            default);
        await _validationReplyService.Received(1).ProcessLlmResponseAndReplyAsync(
            "{\"items\":[]}",
            "reply-token-123",
            "default",
            "U123",
            default);
    }

    [Fact]
    public async Task Handle_EmptyReplyToken_DoesNotProcess()
    {
        var command = CreateCommand(replyToken: "");

        await _handler.Handle(command, default);

        await _lineContentService.DidNotReceive().DownloadContentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyMessageId_RepliesError()
    {
        var lineEvent = new LineEvent(
            Type: "message",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("", "audio", null));
        var command = new ProcessAudioMessageCommand(lineEvent);

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "語音下載失敗，請重新錄音",
            default);
    }

    [Fact]
    public async Task Handle_ContentDownloadFails_RepliesDownloadError()
    {
        _lineContentService.DownloadContentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Download failed"));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "語音下載失敗，請重新錄音",
            default);
    }

    [Fact]
    public async Task Handle_EmptyAudioBytes_RepliesEmptyError()
    {
        SetupAudioDownload(Array.Empty<byte>());
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "語音內容為空，請重新錄音",
            default);
    }

    [Fact]
    public async Task Handle_LlmThrows_RepliesBusyMessage()
    {
        SetupAudioDownload();
        _chatClient.GetResponseAsync(
            Arg.Any<IList<ChatMessage>>(),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Gemini error"));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "系統忙碌中，請稍後重試",
            default);
    }

    [Fact]
    public async Task Handle_SafeReplyThrows_DoesNotThrow()
    {
        _lineContentService.DownloadContentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Download failed"));
        _lineReplyService.ReplyTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Reply failed too"));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        // Should complete without throwing
    }
}
