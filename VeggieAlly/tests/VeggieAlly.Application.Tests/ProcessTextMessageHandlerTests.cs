using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.LineEvents.ProcessText;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Line;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public sealed class ProcessTextMessageHandlerTests
{
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILineReplyService _lineReplyService = Substitute.For<ILineReplyService>();
    private readonly IVegetablePricingService _pricingService = Substitute.For<IVegetablePricingService>();
    private readonly IPriceValidationService _validationService = Substitute.For<IPriceValidationService>();
    private readonly IFlexMessageBuilder _flexMessageBuilder = Substitute.For<IFlexMessageBuilder>();
    private readonly ILogger<ProcessTextMessageHandler> _logger = Substitute.For<ILogger<ProcessTextMessageHandler>>();
    private readonly ProcessTextMessageHandler _handler;

    private const string ValidJson = """{"items":[{"name":"初秋高麗菜","is_new":false,"buy_price":25,"sell_price":35,"quantity":50,"unit":"箱"}]}""";

    public ProcessTextMessageHandlerTests()
    {
        _handler = new ProcessTextMessageHandler(_chatClient, _lineReplyService, _pricingService, _validationService, _flexMessageBuilder, _logger);
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

    private void SetupOkValidation()
    {
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(26m);
        _validationService.Validate(25m, 35m, 26m).Returns(ValidationResult.Ok());
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });
    }

    [Fact]
    public async Task Handle_ValidTextOkValidation_CallsReplyFlexAsync()
    {
        // Arrange
        SetupChatClientReturns(ValidJson);
        SetupOkValidation();
        var command = CreateCommand();

        // Act
        await _handler.Handle(command, default);

        // Assert — now uses ReplyFlexAsync instead of ReplyTextAsync
        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token-123",
            Arg.Is<string>(alt => alt.Contains("1項正常")),
            Arg.Any<object>(),
            default);
    }

    [Fact]
    public async Task Handle_ValidTextAnomalyValidation_CallsReplyFlexAsync()
    {
        // Arrange
        const string lossJson = """{"items":[{"name":"青江菜","is_new":false,"buy_price":100,"sell_price":150,"quantity":10,"unit":"箱"}]}""";
        SetupChatClientReturns(lossJson);
        _pricingService.GetHistoricalAvgPriceAsync("青江菜", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(18m);
        _validationService.Validate(100m, 150m, 18m).Returns(ValidationResult.Anomaly("與歷史均價落差 456%"));
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });
        
        var command = CreateCommand("青江菜 100 賣 150 十箱");

        // Act
        await _handler.Handle(command, default);

        // Assert
        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token-123",
            Arg.Is<string>(alt => alt.Contains("1項異常")),
            Arg.Any<object>(),
            default);
    }

    [Fact]
    public async Task Handle_MultipleItemsMixed_CallsReplyFlexAsync()
    {
        // Arrange
        const string mixedJson = """{"items":[{"name":"初秋高麗菜","is_new":false,"buy_price":25,"sell_price":35,"quantity":50,"unit":"箱"},{"name":"青江菜","is_new":false,"buy_price":50,"sell_price":40,"quantity":10,"unit":"箱"}]}""";
        SetupChatClientReturns(mixedJson);
        
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(26m);
        _pricingService.GetHistoricalAvgPriceAsync("青江菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(18m);
        
        _validationService.Validate(25m, 35m, 26m).Returns(ValidationResult.Ok());
        _validationService.Validate(50m, 40m, 18m).Returns(ValidationResult.Anomaly("售價低於或等於進價"));
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });
        
        var command = CreateCommand();

        // Act
        await _handler.Handle(command, default);

        // Assert
        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token-123",
            Arg.Is<string>(alt => alt.Contains("1項正常") && alt.Contains("1項異常")),
            Arg.Any<object>(),
            default);
    }

    [Fact]
    public async Task Handle_FlexBuilderThrows_FallsBackToTextReply()
    {
        // Arrange
        SetupChatClientReturns(ValidJson);
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(26m);
        _validationService.Validate(25m, 35m, 26m).Returns(ValidationResult.Ok());
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Throws(new InvalidOperationException("Flex build error"));
        
        var command = CreateCommand();

        // Act
        await _handler.Handle(command, default);

        // Assert — falls back to ReplyTextAsync with plain text format
        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            Arg.Is<string>(text => text.Contains("🟢") && text.Contains("初秋高麗菜")),
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
    public async Task Handle_EmptyReplyToken_DoesNotCallLineReplyService()
    {
        SetupChatClientReturns(ValidJson);
        var command = CreateCommand(replyToken: "");

        await _handler.Handle(command, default);

        await _lineReplyService.DidNotReceive().ReplyTextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _lineReplyService.DidNotReceive().ReplyFlexAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidJson_RepliesParsingError()
    {
        SetupChatClientReturns("invalid json");
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token-123",
            "解析失敗，請重新輸入",
            default);
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
    public async Task Handle_LineReplyFlexThrows_LogsWarning()
    {
        SetupChatClientReturns(ValidJson);
        _pricingService.GetHistoricalAvgPriceAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(26m);
        _validationService.Validate(Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal?>()).Returns(ValidationResult.Ok());
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });
        
        _lineReplyService.ReplyFlexAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<object>(),
            Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("LINE API error"));
        var command = CreateCommand();

        await _handler.Handle(command, default);

        // Should complete without throwing
    }

    [Fact]
    public async Task Handle_MarkdownCodeFence_StripsFenceAndProcesses()
    {
        var jsonWithCodeFence = $"```json\n{ValidJson}\n```";
        SetupChatClientReturns(jsonWithCodeFence);
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(26m);
        _validationService.Validate(25m, 35m, 26m).Returns(ValidationResult.Ok());
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });
        
        var command = CreateCommand();

        await _handler.Handle(command, default);

        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token-123",
            Arg.Any<string>(),
            Arg.Any<object>(),
            default);
    }
}