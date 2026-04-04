using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public sealed class ValidationReplyServiceTests
{
    private readonly ILineReplyService _lineReplyService = Substitute.For<ILineReplyService>();
    private readonly IVegetablePricingService _pricingService = Substitute.For<IVegetablePricingService>();
    private readonly IPriceValidationService _validationService = Substitute.For<IPriceValidationService>();
    private readonly IFlexMessageBuilder _flexMessageBuilder = Substitute.For<IFlexMessageBuilder>();
    private readonly ILogger<ValidationReplyService> _logger = Substitute.For<ILogger<ValidationReplyService>>();
    private readonly ValidationReplyService _service;

    private const string ValidJson = """{"items":[{"name":"初秋高麗菜","is_new":false,"buy_price":25,"sell_price":35,"quantity":50,"unit":"箱"}]}""";

    public ValidationReplyServiceTests()
    {
        _service = new ValidationReplyService(_lineReplyService, _pricingService, _validationService, _flexMessageBuilder, _logger);
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
    public async Task ProcessLlmResponse_ValidJsonOk_CallsReplyFlexAsync()
    {
        SetupOkValidation();

        await _service.ProcessLlmResponseAndReplyAsync(ValidJson, "reply-token", default);

        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token",
            Arg.Is<string>(alt => alt.Contains("1項正常")),
            Arg.Any<object>(),
            default);
    }

    [Fact]
    public async Task ProcessLlmResponse_AnomalyValidation_CallsReplyFlexWithAnomaly()
    {
        const string lossJson = """{"items":[{"name":"青江菜","is_new":false,"buy_price":100,"sell_price":150,"quantity":10,"unit":"箱"}]}""";
        _pricingService.GetHistoricalAvgPriceAsync("青江菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(18m);
        _validationService.Validate(100m, 150m, 18m).Returns(ValidationResult.Anomaly("與歷史均價落差 456%"));
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });

        await _service.ProcessLlmResponseAndReplyAsync(lossJson, "reply-token", default);

        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token",
            Arg.Is<string>(alt => alt.Contains("1項異常")),
            Arg.Any<object>(),
            default);
    }

    [Fact]
    public async Task ProcessLlmResponse_MixedItems_CallsReplyFlexWithMixed()
    {
        const string mixedJson = """{"items":[{"name":"初秋高麗菜","is_new":false,"buy_price":25,"sell_price":35,"quantity":50,"unit":"箱"},{"name":"青江菜","is_new":false,"buy_price":50,"sell_price":40,"quantity":10,"unit":"箱"}]}""";
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(26m);
        _pricingService.GetHistoricalAvgPriceAsync("青江菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(18m);
        _validationService.Validate(25m, 35m, 26m).Returns(ValidationResult.Ok());
        _validationService.Validate(50m, 40m, 18m).Returns(ValidationResult.Anomaly("售價低於或等於進價"));
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Returns(new Dictionary<string, object> { ["type"] = "bubble" });

        await _service.ProcessLlmResponseAndReplyAsync(mixedJson, "reply-token", default);

        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token",
            Arg.Is<string>(alt => alt.Contains("1項正常") && alt.Contains("1項異常")),
            Arg.Any<object>(),
            default);
    }

    [Fact]
    public async Task ProcessLlmResponse_NullResponse_RepliesParsingError()
    {
        await _service.ProcessLlmResponseAndReplyAsync(null, "reply-token", default);

        await _lineReplyService.Received(1).ReplyTextAsync("reply-token", "解析失敗，請重新輸入", default);
    }

    [Fact]
    public async Task ProcessLlmResponse_EmptyResponse_RepliesParsingError()
    {
        await _service.ProcessLlmResponseAndReplyAsync("", "reply-token", default);

        await _lineReplyService.Received(1).ReplyTextAsync("reply-token", "解析失敗，請重新輸入", default);
    }

    [Fact]
    public async Task ProcessLlmResponse_InvalidJson_RepliesParsingError()
    {
        await _service.ProcessLlmResponseAndReplyAsync("not json", "reply-token", default);

        await _lineReplyService.Received(1).ReplyTextAsync("reply-token", "解析失敗，請重新輸入", default);
    }

    [Fact]
    public async Task ProcessLlmResponse_EmptyItems_RepliesParsingError()
    {
        await _service.ProcessLlmResponseAndReplyAsync("{\"items\":[]}", "reply-token", default);

        await _lineReplyService.Received(1).ReplyTextAsync("reply-token", "解析失敗，請重新輸入", default);
    }

    [Fact]
    public async Task ProcessLlmResponse_FlexBuilderThrows_FallsBackToText()
    {
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(26m);
        _validationService.Validate(25m, 35m, 26m).Returns(ValidationResult.Ok());
        _flexMessageBuilder.BuildBubble(Arg.Any<IReadOnlyList<ValidatedVegetableItem>>())
            .Throws(new InvalidOperationException("Flex build error"));

        await _service.ProcessLlmResponseAndReplyAsync(ValidJson, "reply-token", default);

        await _lineReplyService.Received(1).ReplyTextAsync(
            "reply-token",
            Arg.Is<string>(text => text.Contains("🟢") && text.Contains("初秋高麗菜")),
            default);
    }

    [Fact]
    public async Task ProcessLlmResponse_ReplyThrows_DoesNotThrow()
    {
        SetupOkValidation();
        _lineReplyService.ReplyFlexAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("LINE API error"));

        await _service.ProcessLlmResponseAndReplyAsync(ValidJson, "reply-token", default);

        // Should complete without throwing
    }

    [Fact]
    public async Task ProcessLlmResponse_MarkdownCodeFence_StripsAndProcesses()
    {
        var jsonWithFence = $"```json\n{ValidJson}\n```";
        SetupOkValidation();

        await _service.ProcessLlmResponseAndReplyAsync(jsonWithFence, "reply-token", default);

        await _lineReplyService.Received(1).ReplyFlexAsync(
            "reply-token",
            Arg.Any<string>(),
            Arg.Any<object>(),
            default);
    }
}
