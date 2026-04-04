using VeggieAlly.Application.Prompts;

namespace VeggieAlly.Application.Tests;

public sealed class SystemPromptsTests
{
    [Theory]
    [InlineData("items")]
    [InlineData("name")]
    [InlineData("is_new")]
    [InlineData("buy_price")]
    [InlineData("sell_price")]
    [InlineData("quantity")]
    [InlineData("unit")]
    public void VegetableParser_ContainsRequiredJsonSchema(string field)
    {
        Assert.Contains(field, SystemPrompts.VegetableParser);
    }

    [Theory]
    [InlineData("初秋高麗菜")]
    [InlineData("改良高麗菜")]
    [InlineData("本地洋蔥")]
    [InlineData("進口洋蔥")]
    [InlineData("白玉苦瓜")]
    [InlineData("綠苦瓜")]
    [InlineData("甜玉米")]
    [InlineData("雙色玉米")]
    [InlineData("生香菇")]
    [InlineData("大蔥")]
    [InlineData("北蔥")]
    [InlineData("粉蔥")]
    [InlineData("老薑")]
    [InlineData("嫩薑")]
    [InlineData("紅心地瓜")]
    [InlineData("黃心地瓜")]
    public void VegetableParser_ContainsAllCategoryItems(string item)
    {
        Assert.Contains(item, SystemPrompts.VegetableParser);
    }

    [Fact]
    public void VegetableParser_ContainsFewShotExamples()
    {
        Assert.Contains("範例", SystemPrompts.VegetableParser);
        Assert.Contains("輸入", SystemPrompts.VegetableParser);
        Assert.Contains("輸出", SystemPrompts.VegetableParser);
        // 至少包含一組 JSON 輸出示例
        Assert.Contains("\"items\"", SystemPrompts.VegetableParser);
        Assert.Contains("\"buy_price\": 25", SystemPrompts.VegetableParser);
    }
}
