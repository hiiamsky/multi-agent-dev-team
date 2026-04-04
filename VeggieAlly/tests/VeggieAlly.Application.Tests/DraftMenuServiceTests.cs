using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public sealed class DraftMenuServiceTests
{
    private readonly IDraftSessionStore _store = Substitute.For<IDraftSessionStore>();
    private readonly IPriceValidationService _validationService = Substitute.For<IPriceValidationService>();
    private readonly IVegetablePricingService _pricingService = Substitute.For<IVegetablePricingService>();
    private readonly ILogger<DraftMenuService> _logger = Substitute.For<ILogger<DraftMenuService>>();
    private readonly DraftMenuService _service;

    private const string TenantId = "default";
    private const string LineUserId = "U_test_user";

    public DraftMenuServiceTests()
    {
        _service = new DraftMenuService(_store, _validationService, _pricingService, _logger);
    }

    private static ValidatedVegetableItem CreateValidatedItem(
        string name, decimal buyPrice, decimal sellPrice, int quantity,
        ValidationStatus status = ValidationStatus.Ok, string? message = null)
    {
        return new ValidatedVegetableItem(
            name, false, buyPrice, sellPrice, quantity, "箱",
            buyPrice,
            new ValidationResult(status, message));
    }

    // --- 11.1 #1 ---
    [Fact]
    public async Task CreateOrMergeDraft_NoDraft_CreatesNew()
    {
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DraftMenuSession?)null);

        var items = new List<ValidatedVegetableItem>
        {
            CreateValidatedItem("初秋高麗菜", 25, 35, 50),
            CreateValidatedItem("青江菜", 18, 28, 30)
        };

        var result = await _service.CreateOrMergeDraftAsync(TenantId, LineUserId, items);

        result.Items.Should().HaveCount(2);
        result.Items.Should().AllSatisfy(i => i.Id.Should().HaveLength(32));
        result.TenantId.Should().Be(TenantId);
        result.LineUserId.Should().Be(LineUserId);
        await _store.Received(1).SaveAsync(Arg.Any<DraftMenuSession>(), Arg.Any<CancellationToken>());
    }

    // --- 11.1 #2 ---
    [Fact]
    public async Task CreateOrMergeDraft_Existing_MergesMatchingByName()
    {
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new("original_id_00000000000000000000", "初秋高麗菜", false, 20, 30, 40, "箱", 20m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);

        var items = new List<ValidatedVegetableItem>
        {
            CreateValidatedItem("初秋高麗菜", 25, 35, 50) // 同名覆寫
        };

        var result = await _service.CreateOrMergeDraftAsync(TenantId, LineUserId, items);

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be("original_id_00000000000000000000"); // 保留原 Id
        result.Items[0].BuyPrice.Should().Be(25);
        result.Items[0].SellPrice.Should().Be(35);
    }

    // --- 11.1 #3 ---
    [Fact]
    public async Task CreateOrMergeDraft_Existing_AppendsUnmatched()
    {
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new("existing_id_0000000000000000000", "初秋高麗菜", false, 20, 30, 40, "箱", 20m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);

        var items = new List<ValidatedVegetableItem>
        {
            CreateValidatedItem("青江菜", 18, 28, 30) // 不匹配，追加
        };

        var result = await _service.CreateOrMergeDraftAsync(TenantId, LineUserId, items);

        result.Items.Should().HaveCount(2);
        result.Items[0].Name.Should().Be("初秋高麗菜");
        result.Items[1].Name.Should().Be("青江菜");
        result.Items[1].Id.Should().HaveLength(32);
    }

    // --- 11.1 #4 ---
    [Fact]
    public async Task CreateOrMergeDraft_EmptyNewItems_KeepsExisting()
    {
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new("existing_id_0000000000000000000", "初秋高麗菜", false, 20, 30, 40, "箱", 20m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);

        var result = await _service.CreateOrMergeDraftAsync(TenantId, LineUserId, new List<ValidatedVegetableItem>());

        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("初秋高麗菜");
    }

    // --- 11.1 #5 ---
    [Fact]
    public async Task CorrectItemPrice_Valid_UpdatesAndRevalidates()
    {
        var itemId = "abc12345678901234567890123456789";
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new(itemId, "初秋高麗菜", false, 50, 40, 50, "箱", 25m, ValidationResult.Anomaly("售價低於或等於進價"))
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(25m);
        _validationService.Validate(25m, 35m, 25m).Returns(ValidationResult.Ok());

        var result = await _service.CorrectItemPriceAsync(TenantId, LineUserId, itemId, 25m, 35m);

        result.BuyPrice.Should().Be(25m);
        result.SellPrice.Should().Be(35m);
        result.Validation.Status.Should().Be(ValidationStatus.Ok);
        await _store.Received(1).SaveAsync(Arg.Any<DraftMenuSession>(), Arg.Any<CancellationToken>());
    }

    // --- 11.1 #6 ---
    [Fact]
    public async Task CorrectItemPrice_OnlyBuyPrice_KeepsSellPrice()
    {
        var itemId = "abc12345678901234567890123456789";
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new(itemId, "初秋高麗菜", false, 50, 40, 50, "箱", 25m, ValidationResult.Anomaly("msg"))
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(25m);
        _validationService.Validate(25m, 40m, 25m).Returns(ValidationResult.Ok());

        var result = await _service.CorrectItemPriceAsync(TenantId, LineUserId, itemId, 25m, null);

        result.BuyPrice.Should().Be(25m);
        result.SellPrice.Should().Be(40m); // 原始售價不變
    }

    // --- 11.1 #7 ---
    [Fact]
    public async Task CorrectItemPrice_OnlySellPrice_KeepsBuyPrice()
    {
        var itemId = "abc12345678901234567890123456789";
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new(itemId, "初秋高麗菜", false, 25, 30, 50, "箱", 25m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(25m);
        _validationService.Validate(25m, 35m, 25m).Returns(ValidationResult.Ok());

        var result = await _service.CorrectItemPriceAsync(TenantId, LineUserId, itemId, null, 35m);

        result.BuyPrice.Should().Be(25m); // 原始進價不變
        result.SellPrice.Should().Be(35m);
    }

    // --- 11.1 #8 ---
    [Fact]
    public async Task CorrectItemPrice_NoDraft_ThrowsInvalidOp()
    {
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DraftMenuSession?)null);

        var act = () => _service.CorrectItemPriceAsync(TenantId, LineUserId, "some_id", 25m, 35m);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- 11.1 #9 ---
    [Fact]
    public async Task CorrectItemPrice_NoItem_ThrowsKeyNotFound()
    {
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new("existing_id_0000000000000000000", "初秋高麗菜", false, 25, 35, 50, "箱", 25m, ValidationResult.Ok())
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);

        var act = () => _service.CorrectItemPriceAsync(TenantId, LineUserId, "non_existent_id", 25m, 35m);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // --- 11.1 #10 ---
    [Fact]
    public async Task CorrectItemPrice_FixAnomaly_BecomesOk()
    {
        var itemId = "abc12345678901234567890123456789";
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>
            {
                new(itemId, "初秋高麗菜", false, 50, 40, 50, "箱", 25m, ValidationResult.Anomaly("售價低於或等於進價"))
            },
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);
        _pricingService.GetHistoricalAvgPriceAsync("初秋高麗菜", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(25m);
        _validationService.Validate(25m, 35m, 25m).Returns(ValidationResult.Ok());

        var result = await _service.CorrectItemPriceAsync(TenantId, LineUserId, itemId, 25m, 35m);

        result.Validation.Status.Should().Be(ValidationStatus.Ok);
    }

    // --- 11.1 #11 ---
    [Fact]
    public async Task GetDraft_Exists_ReturnsSession()
    {
        var existingSession = new DraftMenuSession
        {
            TenantId = TenantId,
            LineUserId = LineUserId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Items = new List<DraftItem>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(existingSession);

        var result = await _service.GetDraftAsync(TenantId, LineUserId);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(TenantId);
    }

    // --- 11.1 #12 ---
    [Fact]
    public async Task GetDraft_Missing_ReturnsNull()
    {
        _store.GetAsync(TenantId, LineUserId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((DraftMenuSession?)null);

        var result = await _service.GetDraftAsync(TenantId, LineUserId);

        result.Should().BeNull();
    }
}
