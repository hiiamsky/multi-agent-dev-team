using FluentAssertions;
using VeggieAlly.Infrastructure.Services;

namespace VeggieAlly.Application.Tests;

public class MockVegetablePricingServiceTests
{
    private readonly MockVegetablePricingService _service = new();

    [Fact]
    public async Task GetHistoricalAvgPriceAsync_KnownItem_ReturnsNonNull()
    {
        // Arrange
        var itemName = "初秋高麗菜";

        // Act
        var result = await _service.GetHistoricalAvgPriceAsync(itemName);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(26m);
    }

    [Fact]
    public async Task GetHistoricalAvgPriceAsync_UnknownItem_ReturnsNull()
    {
        // Arrange
        var itemName = "火星菜";

        // Act
        var result = await _service.GetHistoricalAvgPriceAsync(itemName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoricalAvgPriceAsync_EmptyItem_ReturnsNull()
    {
        // Arrange
        var itemName = "";

        // Act
        var result = await _service.GetHistoricalAvgPriceAsync(itemName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoricalAvgPriceAsync_WhitespaceItem_ReturnsNull()
    {
        // Arrange
        var itemName = "   ";

        // Act
        var result = await _service.GetHistoricalAvgPriceAsync(itemName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoricalAvgPriceAsync_AllKnownItems_ReturnExpectedPrices()
    {
        // Arrange & Act & Assert
        var expectedPrices = new Dictionary<string, decimal>
        {
            ["初秋高麗菜"] = 26,
            ["小白菜"] = 15,
            ["青江菜"] = 18,
            ["空心菜"] = 20,
            ["白蘿蔔"] = 12,
            ["紅蘿蔔"] = 22,
            ["牛番茄"] = 30,
            ["金針菇"] = 25
        };

        foreach (var kvp in expectedPrices)
        {
            var result = await _service.GetHistoricalAvgPriceAsync(kvp.Key);
            result.Should().Be(kvp.Value, $"expected price for {kvp.Key}");
        }
    }
}