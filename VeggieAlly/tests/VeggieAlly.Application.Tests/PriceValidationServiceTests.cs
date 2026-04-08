using FluentAssertions;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Tests;

public class PriceValidationServiceTests
{
    private readonly PriceValidationService _service = new();

    [Fact]
    public void Validate_NormalProfit_ReturnsOk()
    {
        // Arrange
        var buyPrice = 25m;
        var sellPrice = 35m;
        var historicalAvgPrice = 26m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Ok);
        result.Message.Should().BeNull();
    }

    [Fact]
    public void Validate_SellPriceEqualsToBuyPrice_ReturnsAnomaly()
    {
        // Arrange
        var buyPrice = 30m;
        var sellPrice = 30m;
        var historicalAvgPrice = 28m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Anomaly);
        result.Message.Should().Be("售價低於或等於進價");
    }

    [Fact]
    public void Validate_SellPriceLowerThanBuyPrice_ReturnsAnomaly()
    {
        // Arrange
        var buyPrice = 50m;
        var sellPrice = 40m;
        var historicalAvgPrice = 48m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Anomaly);
        result.Message.Should().Be("售價低於或等於進價");
    }

    [Fact]
    public void Validate_HistoricalVariation31PercentUp_ReturnsAnomaly()
    {
        // Arrange
        var buyPrice = 131m;
        var sellPrice = 180m;
        var historicalAvgPrice = 100m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Anomaly);
        result.Message.Should().StartWith("與歷史均價落差");
    }

    [Fact]
    public void Validate_HistoricalVariation31PercentDown_ReturnsAnomaly()
    {
        // Arrange
        var buyPrice = 69m;
        var sellPrice = 90m;
        var historicalAvgPrice = 100m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Anomaly);
        result.Message.Should().StartWith("與歷史均價落差");
    }

    [Fact]
    public void Validate_Exactly30PercentVariation_ReturnsOk()
    {
        // Arrange
        var buyPrice = 130m;
        var sellPrice = 180m;
        var historicalAvgPrice = 100m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Ok);
        result.Message.Should().BeNull();
    }

    [Fact]
    public void Validate_NoHistoricalPriceButNormalProfit_ReturnsOk()
    {
        // Arrange
        var buyPrice = 25m;
        var sellPrice = 35m;
        decimal? historicalAvgPrice = null;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Ok);
        result.Message.Should().BeNull();
    }

    [Fact]
    public void Validate_NoHistoricalPriceButLoss_ReturnsAnomaly()
    {
        // Arrange
        var buyPrice = 50m;
        var sellPrice = 40m;
        decimal? historicalAvgPrice = null;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Anomaly);
        result.Message.Should().Be("售價低於或等於進價");
    }

    [Fact]
    public void Validate_SellPriceIsZero_ReturnsOk()
    {
        // Arrange
        var buyPrice = 50m;
        var sellPrice = 0m; // 未設定售價
        var historicalAvgPrice = 48m;

        // Act
        var result = _service.Validate(buyPrice, sellPrice, historicalAvgPrice);

        // Assert
        result.Status.Should().Be(ValidationStatus.Ok);
        result.Message.Should().BeNull();
    }
}