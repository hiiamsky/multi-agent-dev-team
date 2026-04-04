using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.LineEvents.ProcessAudio;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Application.Menu.Publish;
using VeggieAlly.Domain.Models.Line;
using VeggieAlly.WebAPI.Controllers;

namespace VeggieAlly.Application.Tests;

public sealed class WebhookControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ITenantConfigService _tenantConfigService = Substitute.For<ITenantConfigService>();
    private readonly ILogger<WebhookController> _logger = Substitute.For<ILogger<WebhookController>>();
    private readonly WebhookController _controller;

    public WebhookControllerTests()
    {
        _tenantConfigService.GetTenantId().Returns("default");
        _controller = new WebhookController(_mediator, _tenantConfigService, _logger);
    }

    [Fact]
    public async Task Receive_EmptyEvents_Returns200()
    {
        var payload = new LineWebhookPayload([]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.DidNotReceive().Send(
            Arg.Any<ProcessTextMessageCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_NullPayload_Returns200()
    {
        var result = await _controller.Receive(null!);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Receive_TextMessage_DispatchesTextCommand()
    {
        var textEvent = new LineEvent(
            Type: "message",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("msg1", "text", "高麗菜 25"));
        var payload = new LineWebhookPayload([textEvent]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.Received(1).Send(
            Arg.Is<ProcessTextMessageCommand>(c => c.Event == textEvent),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_AudioMessage_DispatchesAudioCommand()
    {
        var audioEvent = new LineEvent(
            Type: "message",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("audio-msg-001", "audio", null));
        var payload = new LineWebhookPayload([audioEvent]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.Received(1).Send(
            Arg.Is<ProcessAudioMessageCommand>(c => c.Event == audioEvent),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_NonSupportedMessage_SkipsEvent()
    {
        var imageEvent = new LineEvent(
            Type: "message",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("msg1", "image", null));
        var payload = new LineWebhookPayload([imageEvent]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.DidNotReceive().Send(
            Arg.Any<ProcessTextMessageCommand>(),
            Arg.Any<CancellationToken>());
        await _mediator.DidNotReceive().Send(
            Arg.Any<ProcessAudioMessageCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_NoReplyToken_SkipsEvent()
    {
        var noTokenEvent = new LineEvent(
            Type: "message",
            ReplyToken: null,
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("msg1", "text", "高麗菜 25"));
        var payload = new LineWebhookPayload([noTokenEvent]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.DidNotReceive().Send(
            Arg.Any<ProcessTextMessageCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_TextHandlerThrows_StillReturns200()
    {
        var textEvent = new LineEvent(
            Type: "message",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("msg1", "text", "高麗菜 25"));
        var payload = new LineWebhookPayload([textEvent]);

        _mediator.Send(
            Arg.Any<ProcessTextMessageCommand>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Handler 爆了"));

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Receive_AudioHandlerThrows_StillReturns200()
    {
        var audioEvent = new LineEvent(
            Type: "message",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: new LineMessage("audio-msg-001", "audio", null));
        var payload = new LineWebhookPayload([audioEvent]);

        _mediator.Send(
            Arg.Any<ProcessAudioMessageCommand>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Audio Handler 爆了"));

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Receive_PostbackPublish_DispatchesPublishCommand()
    {
        var postbackEvent = new LineEvent(
            Type: "postback",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: null,
            Postback: new LinePostback("action=publish"));
        var payload = new LineWebhookPayload([postbackEvent]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.Received(1).Send(
            Arg.Is<PublishMenuCommand>(c => c.TenantId == "default" && c.LineUserId == "U123"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_PostbackUnknownAction_Skips()
    {
        var postbackEvent = new LineEvent(
            Type: "postback",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: null,
            Postback: new LinePostback("action=unknown_thing"));
        var payload = new LineWebhookPayload([postbackEvent]);

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
        await _mediator.DidNotReceive().Send(
            Arg.Any<PublishMenuCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Receive_PostbackHandlerThrows_StillReturns200()
    {
        var postbackEvent = new LineEvent(
            Type: "postback",
            ReplyToken: "reply-token-123",
            Source: new LineEventSource("user", "U123"),
            Message: null,
            Postback: new LinePostback("action=publish"));
        var payload = new LineWebhookPayload([postbackEvent]);

        _mediator.Send(
            Arg.Any<PublishMenuCommand>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Publish Handler 爆了"));

        var result = await _controller.Receive(payload);

        Assert.IsType<OkResult>(result);
    }
}
