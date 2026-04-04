using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.ValueObjects;

namespace VeggieAlly.Application.Services;

public sealed class FlexMessageBuilder : IFlexMessageBuilder
{
    public object BuildBubble(IReadOnlyList<ValidatedVegetableItem> items)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException("品項清單不得為空", nameof(items));

        var okItems = items.Where(i => i.Validation.Status == ValidationStatus.Ok).ToList();
        var anomalyItems = items.Where(i => i.Validation.Status != ValidationStatus.Ok).ToList();

        var bodyContents = new List<object>();

        // 🟢 準備發布區
        if (okItems.Count > 0)
        {
            bodyContents.Add(CreateSectionHeader("🟢 準備發布", "#1DB446"));
            bodyContents.Add(CreateSeparator());
            foreach (var item in okItems)
            {
                bodyContents.Add(CreateItemRow(item));
            }
        }

        // 🔴 異常待處理區
        if (anomalyItems.Count > 0)
        {
            if (okItems.Count > 0)
                bodyContents.Add(CreateSeparator("lg"));

            bodyContents.Add(CreateSectionHeader("🔴 異常待處理", "#FF0000"));
            bodyContents.Add(CreateSeparator());
            foreach (var item in anomalyItems)
            {
                bodyContents.Add(CreateAnomalyItemBox(item));
            }
        }

        var bubble = new Dictionary<string, object>
        {
            ["type"] = "bubble",
            ["header"] = new Dictionary<string, object>
            {
                ["type"] = "box",
                ["layout"] = "vertical",
                ["contents"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = "📋 今日報價確認",
                        ["weight"] = "bold",
                        ["size"] = "lg",
                        ["color"] = "#333333"
                    }
                }
            },
            ["body"] = new Dictionary<string, object>
            {
                ["type"] = "box",
                ["layout"] = "vertical",
                ["spacing"] = "sm",
                ["contents"] = bodyContents
            },
            ["footer"] = new Dictionary<string, object>
            {
                ["type"] = "box",
                ["layout"] = "vertical",
                ["contents"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = "💡 如需修正，請重新傳送語音或文字",
                        ["size"] = "xs",
                        ["color"] = "#AAAAAA",
                        ["align"] = "center"
                    }
                }
            }
        };

        return bubble;
    }

    private static Dictionary<string, object> CreateSectionHeader(string text, string color)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "text",
            ["text"] = text,
            ["weight"] = "bold",
            ["color"] = color,
            ["size"] = "md",
            ["margin"] = "md"
        };
    }

    private static Dictionary<string, object> CreateSeparator(string? margin = null)
    {
        var sep = new Dictionary<string, object>
        {
            ["type"] = "separator"
        };
        if (margin is not null)
            sep["margin"] = margin;
        return sep;
    }

    private static Dictionary<string, object> CreateItemRow(ValidatedVegetableItem item)
    {
        return new Dictionary<string, object>
        {
            ["type"] = "box",
            ["layout"] = "horizontal",
            ["margin"] = "sm",
            ["contents"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = item.Name,
                    ["size"] = "sm",
                    ["color"] = "#333333",
                    ["flex"] = 3
                },
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = $"進${item.BuyPrice} 售${item.SellPrice} x{item.Quantity}{item.Unit}",
                    ["size"] = "sm",
                    ["color"] = "#666666",
                    ["align"] = "end",
                    ["flex"] = 5
                }
            }
        };
    }

    private static Dictionary<string, object> CreateAnomalyItemBox(ValidatedVegetableItem item)
    {
        var contents = new List<object>
        {
            new Dictionary<string, object>
            {
                ["type"] = "box",
                ["layout"] = "horizontal",
                ["contents"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = item.Name,
                        ["size"] = "sm",
                        ["color"] = "#333333",
                        ["flex"] = 3
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = $"進${item.BuyPrice} 售${item.SellPrice} x{item.Quantity}{item.Unit}",
                        ["size"] = "sm",
                        ["color"] = "#666666",
                        ["align"] = "end",
                        ["flex"] = 5
                    }
                }
            }
        };

        if (!string.IsNullOrEmpty(item.Validation.Message))
        {
            contents.Add(new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = $"⚠️ {item.Validation.Message}",
                ["size"] = "xs",
                ["color"] = "#FF0000",
                ["margin"] = "xs"
            });
        }

        return new Dictionary<string, object>
        {
            ["type"] = "box",
            ["layout"] = "vertical",
            ["margin"] = "sm",
            ["contents"] = contents
        };
    }
}
