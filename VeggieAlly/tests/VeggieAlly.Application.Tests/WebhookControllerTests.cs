using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Domain.Models.Line;
using VeggieAlly.WebAPI.Controllers;

namespace VeggieAlly.Application.Tests;

public sealed class WebhookControllerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<WebhookController> _logger = Substitute.For<ILogger<WebhookController>>();
    private readonly WebhookController _controller;

    public WebhookControllerTests()
    {
        _controller = new WebhookController(_mediator, _logger);
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
    public async Task Receive_TextMessage_DispatchesCommand()
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
    public async Task Receive_NonTextMessage_SkipsEvent()
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
    public async Task Receive_HandlerThrows_StillReturns200()
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
}
