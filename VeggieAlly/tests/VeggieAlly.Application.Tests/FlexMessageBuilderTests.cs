using System.Text.Json;
using FluentAssertions;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.Models.Draft;
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

    // --- BuildDraftBubble Tests ---

    private static DraftMenuSession CreateDraftSession(params DraftItem[] items)
    {
        return new DraftMenuSession
        {
            TenantId = "default",
            LineUserId = "U123",
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = items.ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public void BuildDraftBubble_Anomaly_HasButtons()
    {
        var session = CreateDraftSession(
            new DraftItem("a1b2c3d4e5f67890a1b2c3d4e5f67890", "初秋高麗菜", false, 50, 40, 10, "箱", 25m,
                ValidationResult.Anomaly("售價低於或等於進價")));

        var json = Serialize(_builder.BuildDraftBubble(session, "https://liff.line.me/1234"));

        json.Should().Contain("✏️ 修正");
        json.Should().Contain("item_id=a1b2c3d4e5f67890a1b2c3d4e5f67890");
        json.Should().Contain("uri");
    }

    [Fact]
    public void BuildDraftBubble_AllOk_NoButtons()
    {
        var session = CreateDraftSession(
            new DraftItem("a1b2c3d4e5f67890a1b2c3d4e5f67890", "初秋高麗菜", false, 25, 35, 50, "箱", 25m,
                ValidationResult.Ok()));

        var json = Serialize(_builder.BuildDraftBubble(session, "https://liff.line.me/1234"));

        json.Should().NotContain("✏️ 修正");
        json.Should().Contain("初秋高麗菜");
        json.Should().Contain("準備發布");
    }

    [Fact]
    public void BuildDraftBubble_Empty_Throws()
    {
        var session = CreateDraftSession();

        var act = () => _builder.BuildDraftBubble(session, null);

        act.Should().Throw<ArgumentException>();
    }

    // ── P3-002 新增測試 ──

    [Fact]
    public void BuildDraftBubble_AllOk_HasPublishButton()
    {
        var session = CreateDraftSession(
            new DraftItem("id1", "高麗菜", false, 25, 35, 50, "箱", 24m, ValidationResult.Ok()),
            new DraftItem("id2", "白蘿蔔", false, 18, 30, 30, "箱", 20m, ValidationResult.Ok()));

        var result = _builder.BuildDraftBubble(session, "https://liff.example.com");
        var json = Serialize(result);

        // Footer 含 Postback 按鈕
        json.Should().Contain("postback");
        json.Should().Contain("一鍵發布");
        json.Should().Contain("action=publish");
    }

    [Fact]
    public void BuildDraftBubble_HasAnomaly_NoPublishButton()
    {
        var session = CreateDraftSession(
            new DraftItem("id1", "高麗菜", false, 25, 35, 50, "箱", 24m, ValidationResult.Ok()),
            new DraftItem("id2", "白蘿蔔", false, 30, 20, 30, "箱", 20m, ValidationResult.Anomaly("售價低於進價")));

        var result = _builder.BuildDraftBubble(session, "https://liff.example.com");
        var json = Serialize(result);

        json.Should().NotContain("🚀 一鍵發布");
        json.Should().Contain("點擊修正按鈕或重新傳送語音修正");
    }

    [Fact]
    public void BuildPublishedBubble_ShowsConfirmation()
    {
        var menu = new VeggieAlly.Domain.Models.Menu.PublishedMenu
        {
            Id = "menu-1",
            TenantId = "tenant-1",
            PublishedByUserId = "user-1",
            Date = DateOnly.FromDateTime(DateTime.Today),
            Items = [new VeggieAlly.Domain.Models.Menu.PublishedMenuItem
            {
                Id = "item-1",
                MenuId = "menu-1",
                Name = "高麗菜",
                SellPrice = 35m,
                BuyPrice = 25m,
                OriginalQty = 50,
                RemainingQty = 50,
                Unit = "箱"
            }],
            PublishedAt = DateTimeOffset.UtcNow
        };

        var result = _builder.BuildPublishedBubble(menu);
        var json = Serialize(result);

        json.Should().Contain("✅");
        json.Should().Contain("菜單發布成功");
        json.Should().Contain("高麗菜");
    }
}
