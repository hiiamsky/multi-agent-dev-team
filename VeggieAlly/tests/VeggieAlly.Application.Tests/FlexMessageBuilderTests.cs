using System.Text.Json;
using FluentAssertions;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public sealed class FlexMessageBuilderTests
{
    private readonly FlexMessageBuilder _builder = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static ValidatedVegetableItem CreateItem(
        string name, decimal buyPrice, decimal sellPrice, int quantity,
        ValidationStatus status, string? message = null)
    {
        return new ValidatedVegetableItem(
            name, false, buyPrice, sellPrice, quantity, "箱",
            buyPrice,
            new ValidationResult(status, message));
    }

    private string Serialize(object result) => JsonSerializer.Serialize(result, JsonOptions);

    [Fact]
    public void BuildBubble_AllOk_HasGreenSectionOnly()
    {
        var items = new List<ValidatedVegetableItem>
        {
            CreateItem("初秋高麗菜", 25, 35, 50, ValidationStatus.Ok),
            CreateItem("青江菜", 18, 28, 30, ValidationStatus.Ok)
        };

        var json = Serialize(_builder.BuildBubble(items));

        json.Should().Contain("準備發布");
        json.Should().NotContain("異常待處理");
        json.Should().Contain("初秋高麗菜");
        json.Should().Contain("青江菜");
    }

    [Fact]
    public void BuildBubble_AllAnomaly_HasRedSectionOnly()
    {
        var items = new List<ValidatedVegetableItem>
        {
            CreateItem("初秋高麗菜", 50, 40, 10, ValidationStatus.Anomaly, "售價低於或等於進價"),
            CreateItem("青江菜", 100, 80, 5, ValidationStatus.Anomaly, "售價低於或等於進價")
        };

        var json = Serialize(_builder.BuildBubble(items));

        json.Should().Contain("異常待處理");
        json.Should().NotContain("準備發布");
        json.Should().Contain("售價低於或等於進價");
    }

    [Fact]
    public void BuildBubble_MixedItems_HasBothSections()
    {
        var items = new List<ValidatedVegetableItem>
        {
            CreateItem("初秋高麗菜", 25, 35, 50, ValidationStatus.Ok),
            CreateItem("青江菜", 50, 40, 10, ValidationStatus.Anomaly, "售價低於或等於進價")
        };

        var json = Serialize(_builder.BuildBubble(items));

        json.Should().Contain("準備發布");
        json.Should().Contain("異常待處理");
    }

    [Fact]
    public void BuildBubble_SingleOkItem_StructureComplete()
    {
        var items = new List<ValidatedVegetableItem>
        {
            CreateItem("初秋高麗菜", 25, 35, 50, ValidationStatus.Ok)
        };

        var json = Serialize(_builder.BuildBubble(items));

        json.Should().Contain("bubble");
        json.Should().Contain("今日報價確認");
        json.Should().Contain("如需修正");
        json.Should().Contain("初秋高麗菜");
    }

    [Fact]
    public void BuildBubble_SingleAnomalyItem_ShowsWarning()
    {
        var items = new List<ValidatedVegetableItem>
        {
            CreateItem("青江菜", 100, 150, 10, ValidationStatus.Anomaly, "與歷史均價落差 456%")
        };

        var json = Serialize(_builder.BuildBubble(items));

        json.Should().Contain("與歷史均價落差 456%");
        json.Should().Contain("⚠️");
    }

    [Fact]
    public void BuildBubble_EmptyList_ThrowsArgumentException()
    {
        var items = new List<ValidatedVegetableItem>();

        var act = () => _builder.BuildBubble(items);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BuildBubble_ResultIsJsonSerializable()
    {
        var items = new List<ValidatedVegetableItem>
        {
            CreateItem("初秋高麗菜", 25, 35, 50, ValidationStatus.Ok),
            CreateItem("青江菜", 50, 40, 10, ValidationStatus.Anomaly, "售價低於或等於進價")
        };

        var result = _builder.BuildBubble(items);
        var json = Serialize(result);
        json.Should().NotBeNullOrWhiteSpace();

        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("type").GetString().Should().Be("bubble");
    }
}
