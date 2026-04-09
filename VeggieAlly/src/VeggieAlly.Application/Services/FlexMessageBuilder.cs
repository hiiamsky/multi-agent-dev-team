using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Services.FlexMessage;
using VeggieAlly.Domain.ValueObjects;
using VeggieAlly.Domain.Models.Draft;
using VeggieAlly.Domain.Models.Menu;

namespace VeggieAlly.Application.Services;

public sealed class FlexMessageBuilder : IFlexMessageBuilder
{
    /// <summary>
    /// 使用強型別建構 Flex Message Bubble（推薦）
    /// </summary>
    public FlexBubble BuildStrongTypedBubble(IReadOnlyList<ValidatedVegetableItem> items)
    {
        if (items is null || items.Count == 0)
            throw new ArgumentException("品項清單不得為空", nameof(items));

        var okItems = items.Where(i => i.Validation.Status == ValidationStatus.Ok).ToList();
        var anomalyItems = items.Where(i => i.Validation.Status != ValidationStatus.Ok).ToList();

        var bodyContents = new List<IFlexComponent>();

        // 🟢 準備發布區
        if (okItems.Count > 0)
        {
            bodyContents.Add(CreateStrongTypedSectionHeader("🟢 準備發布", "#1DB446"));
            bodyContents.Add(new FlexSeparator());
            foreach (var item in okItems)
            {
                bodyContents.Add(CreateStrongTypedItemRow(item.Name, item.BuyPrice, item.SellPrice, item.Quantity, item.Unit));
            }
        }

        // 🔴 異常待處理區
        if (anomalyItems.Count > 0)
        {
            if (okItems.Count > 0)
                bodyContents.Add(new FlexSeparator { Margin = "lg" });

            bodyContents.Add(CreateStrongTypedSectionHeader("🔴 異常待處理", "#FF0000"));
            bodyContents.Add(new FlexSeparator());
            foreach (var item in anomalyItems)
            {
                bodyContents.Add(CreateStrongTypedAnomalyItemBox(item.Name, item.BuyPrice, item.SellPrice, item.Quantity, item.Unit, item.Validation.Message));
            }
        }

        return new FlexBubble
        {
            Header = new FlexBox
            {
                Layout = "vertical",
                Contents =
                [
                    new FlexText
                    {
                        Text = "📋 今日報價確認",
                        Weight = "bold",
                        Size = "lg",
                        Color = "#333333"
                    }
                ]
            },
            Body = new FlexBox
            {
                Layout = "vertical",
                Spacing = "sm",
                Contents = bodyContents
            },
            Footer = new FlexBox
            {
                Layout = "vertical",
                Contents =
                [
                    new FlexText
                    {
                        Text = "💡 如需修正，請重新傳送語音或文字",
                        Size = "xs",
                        Color = "#AAAAAA",
                        Align = "center"
                    }
                ]
            }
        };
    }

    /// <summary>
    /// 建構 Flex Message Bubble（維持向後相容）
    /// </summary>
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

        // Footer 根據情況顯示不同內容
        var footerContents = new List<object>();
        
        if (anomalyItems.Count == 0 && okItems.Count > 0)
        {
            // 全部品項都 Ok，顯示發布按鈕
            footerContents.Add(new Dictionary<string, object>
            {
                ["type"] = "button",
                ["height"] = "sm",
                ["style"] = "primary",
                ["color"] = "#1DB446",
                ["action"] = new Dictionary<string, object>
                {
                    ["type"] = "postback",
                    ["label"] = "🚀 一鍵發布",
                    ["data"] = "action=publish"
                }
            });
        }
        else
        {
            // 有異常品項，顯示修正提示
            var footerText = string.IsNullOrWhiteSpace(liffBaseUrl)
                ? "💡 如需修正，請重新傳送語音或文字"
                : "💡 點擊修正按鈕或重新傳送語音修正";
                
            footerContents.Add(new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = footerText,
                ["size"] = "xs",
                ["color"] = "#AAAAAA",
                ["align"] = "center"
            });
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
                ["contents"] = footerContents
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
            // 建立兩個按鈕：修正進貨價、修正售價
            contents.Add(new Dictionary<string, object>
            {
                ["type"] = "box",
                ["layout"] = "horizontal",
                ["spacing"] = "sm",
                ["margin"] = "xs",
                ["contents"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["height"] = "sm",
                        ["style"] = "secondary",
                        ["action"] = new Dictionary<string, object>
                        {
                            ["type"] = "uri",
                            ["label"] = "✏️ 修正進價",
                            ["uri"] = $"{liffBaseUrl}/numpad?itemId={Uri.EscapeDataString(item.Id.ToString())}&itemName={Uri.EscapeDataString(item.Name)}&buyPrice={Uri.EscapeDataString(item.BuyPrice.ToString(System.Globalization.CultureInfo.InvariantCulture))}&sellPrice={Uri.EscapeDataString(item.SellPrice.ToString(System.Globalization.CultureInfo.InvariantCulture))}&field=buy_price"
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "button",
                        ["height"] = "sm",
                        ["style"] = "secondary",
                        ["action"] = new Dictionary<string, object>
                        {
                            ["type"] = "uri",
                            ["label"] = "✏️ 修正售價",
                            ["uri"] = $"{liffBaseUrl}/numpad?itemId={Uri.EscapeDataString(item.Id.ToString())}&itemName={Uri.EscapeDataString(item.Name)}&buyPrice={Uri.EscapeDataString(item.BuyPrice.ToString(System.Globalization.CultureInfo.InvariantCulture))}&sellPrice={Uri.EscapeDataString(item.SellPrice.ToString(System.Globalization.CultureInfo.InvariantCulture))}&field=sell_price"
                        }
                    }
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

    public object BuildPublishedBubble(PublishedMenu menu)
    {
        if (menu?.Items is null || menu.Items.Count == 0)
            throw new ArgumentException("已發布菜單不得為空", nameof(menu));

        var bodyContents = new List<object>
        {
            // 成功訊息
            new Dictionary<string, object>
            {
                ["type"] = "box",
                ["layout"] = "horizontal",
                ["contents"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = "✅",
                        ["size"] = "xxl",
                        ["flex"] = 1
                    },
                    new Dictionary<string, object>
                    {
                        ["type"] = "box",
                        ["layout"] = "vertical",
                        ["flex"] = 4,
                        ["contents"] = new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "text",
                                ["text"] = "菜單發布成功！",
                                ["weight"] = "bold",
                                ["size"] = "lg",
                                ["color"] = "#1DB446"
                            },
                            new Dictionary<string, object>
                            {
                                ["type"] = "text",
                                ["text"] = $"共 {menu.Items.Count} 項商品",
                                ["size"] = "sm",
                                ["color"] = "#666666",
                                ["margin"] = "xs"
                            }
                        }
                    }
                }
            },
            CreateSeparator("lg")
        };

        // 商品清單（限制顯示前5項）
        var displayItems = menu.Items.Take(5);
        foreach (var item in displayItems)
        {
            bodyContents.Add(CreatePublishedItemRow(item));
        }

        // 如果商品超過5項，顯示省略提示
        if (menu.Items.Count > 5)
        {
            bodyContents.Add(new Dictionary<string, object>
            {
                ["type"] = "text",
                ["text"] = $"... 等共 {menu.Items.Count} 項商品",
                ["size"] = "xs",
                ["color"] = "#AAAAAA",
                ["align"] = "center",
                ["margin"] = "md"
            });
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
                        ["text"] = "🎉 發布完成",
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
                        ["text"] = "顧客現在可以透過 LIFF 查看今日菜單",
                        ["size"] = "xs",
                        ["color"] = "#AAAAAA",
                        ["align"] = "center"
                    }
                }
            }
        };

        return bubble;
    }

    private static Dictionary<string, object> CreatePublishedItemRow(PublishedMenuItem item)
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
                    ["text"] = $"${item.SellPrice} x{item.RemainingQty}{item.Unit}",
                    ["size"] = "sm",
                    ["color"] = "#666666",
                    ["align"] = "end",
                    ["flex"] = 2
                }
            }
        };
    }

    /// <summary>
    /// 創建強型別區塊標題
    /// </summary>
    private static FlexText CreateStrongTypedSectionHeader(string title, string color)
    {
        return new FlexText
        {
            Text = title,
            Weight = "bold",
            Size = "md",
            Color = color
        };
    }

    /// <summary>
    /// 創建強型別品項行
    /// </summary>
    private static FlexBox CreateStrongTypedItemRow(string name, decimal buyPrice, decimal sellPrice, int quantity, string unit)
    {
        return new FlexBox
        {
            Layout = "horizontal",
            Margin = "sm",
            Contents =
            [
                new FlexText
                {
                    Text = name,
                    Size = "sm",
                    Color = "#333333",
                    Flex = 3
                },
                new FlexText
                {
                    Text = $"進${buyPrice} 售${sellPrice}",
                    Size = "xs",
                    Color = "#666666",
                    Align = "end",
                    Flex = 2
                },
                new FlexText
                {
                    Text = $"{quantity}{unit}",
                    Size = "xs",
                    Color = "#666666",
                    Align = "end",
                    Flex = 1
                }
            ]
        };
    }

    /// <summary>
    /// 創建強型別異常品項框
    /// </summary>
    private static FlexBox CreateStrongTypedAnomalyItemBox(string name, decimal buyPrice, decimal sellPrice, int quantity, string unit, string? errorMessage)
    {
        var contents = new List<IFlexComponent>
        {
            new FlexBox
            {
                Layout = "horizontal",
                Contents =
                [
                    new FlexText
                    {
                        Text = name,
                        Size = "sm",
                        Color = "#FF0000",
                        Weight = "bold",
                        Flex = 3
                    },
                    new FlexText
                    {
                        Text = $"進${buyPrice} 售${sellPrice}",
                        Size = "xs",
                        Color = "#666666",
                        Align = "end",
                        Flex = 2
                    },
                    new FlexText
                    {
                        Text = $"{quantity}{unit}",
                        Size = "xs",
                        Color = "#666666",
                        Align = "end",
                        Flex = 1
                    }
                ]
            }
        };

        if (!string.IsNullOrEmpty(errorMessage))
        {
            contents.Add(new FlexText
            {
                Text = $"⚠️ {errorMessage}",
                Size = "xxs",
                Color = "#FF6600",
                Margin = "xs",
                Wrap = true
            });
        }

        return new FlexBox
        {
            Layout = "vertical",
            Margin = "sm",
            PaddingAll = "xs",
            BackgroundColor = "#FFF5F5",
            Contents = contents
        };
    }
}
