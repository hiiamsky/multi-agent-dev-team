using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Domain.ValueObjects;
using VeggieAlly.Domain.Models.Draft;

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
                bodyContents.Add(CreateItemRow(item.Name, item.BuyPrice, item.SellPrice, item.Quantity, item.Unit));
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
                bodyContents.Add(CreateAnomalyItemBox(item.Name, item.BuyPrice, item.SellPrice, item.Quantity, item.Unit, item.Validation.Message));
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

    public object BuildDraftBubble(DraftMenuSession session, string? liffBaseUrl)
    {
        if (session?.Items is null || session.Items.Count == 0)
            throw new ArgumentException("草稿 Session 不得為空", nameof(session));

        var okItems = session.Items.Where(i => i.Validation.Status == ValidationStatus.Ok).ToList();
        var anomalyItems = session.Items.Where(i => i.Validation.Status != ValidationStatus.Ok).ToList();

        var bodyContents = new List<object>();

        // 🟢 準備發布區
        if (okItems.Count > 0)
        {
            bodyContents.Add(CreateSectionHeader("🟢 準備發布", "#1DB446"));
            bodyContents.Add(CreateSeparator());
            foreach (var item in okItems)
            {
                bodyContents.Add(CreateItemRow(item.Name, item.BuyPrice, item.SellPrice, item.Quantity, item.Unit));
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
                bodyContents.Add(CreateDraftAnomalyItemBox(item, liffBaseUrl));
            }
        }

        var footerText = string.IsNullOrWhiteSpace(liffBaseUrl)
            ? "💡 如需修正，請重新傳送語音或文字"
            : "💡 點擊修正按鈕或重新傳送語音修正";

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
                        ["text"] = footerText,
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

    private static Dictionary<string, object> CreateItemRow(string name, decimal buyPrice, decimal sellPrice, int quantity, string unit)
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
                    ["text"] = name,
                    ["size"] = "sm",
                    ["color"] = "#333333",
                    ["flex"] = 3
                },
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = $"進${buyPrice} 售${sellPrice} x{quantity}{unit}",
                    ["size"] = "sm",
                    ["color"] = "#666666",
                    ["align"] = "end",
                    ["flex"] = 5
                }
            }
        };
    }

    private static Dictionary<string, object> CreateAnomalyItemBox(string name, decimal buyPrice, decimal sellPrice, int quantity, string unit, string? validationMessage)
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
                        ["text"] = name,
                        ["size"] = "sm",
                        ["color"] = "#333333",
                        ["flex"] = 3
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = $"進${buyPrice} 售${sellPrice} x{quantity}{unit}",
                        ["size"] = "sm",
                        ["color"] = "#666666",
                        ["align"] = "end",
                        ["flex"] = 5
                    }
                }
            }
        };

        if (!string.IsNullOrEmpty(validationMessage))
        {
            contents.Add(new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = $"⚠️ {validationMessage}",
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

    private static Dictionary<string, object> CreateDraftAnomalyItemBox(DraftItem item, string? liffBaseUrl)
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

        // 添加 LIFF 修正按鈕 (如果有 liffBaseUrl)
        if (!string.IsNullOrWhiteSpace(liffBaseUrl))
        {
            contents.Add(new Dictionary<string, object>
            {
                ["type"] = "button",
                ["height"] = "sm",
                ["style"] = "secondary",
                ["action"] = new Dictionary<string, object>
                {
                    ["type"] = "uri",
                    ["label"] = "✏️ 修正",
                    ["uri"] = $"{liffBaseUrl}?item_id={item.Id}&field=buy_price"
                }
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
