# P2-002 SA/SD 規格藍圖：LINE Flex Message 紅綠燈卡片

## 1. 目標

將 P2-001 的純文字 emoji 回覆（`📋 報價驗證結果：\n🟢 ... 🔴 ...`）升級為 LINE Flex Message **BubbleContainer**，以結構化卡片呈現紅綠燈分區。

---

## 2. Architecture Delta（與 P2-001 差異）

```
ProcessTextMessageHandler
  └─ ProcessValidationAsync() → List<ValidatedVegetableItem>
      └─ 【P2-001】GenerateValidationReply() → string  ← 移除
      └─ 【P2-002】IFlexMessageBuilder.Build()         ← 新增
          ↓
      ILineReplyService.ReplyFlexAsync()               ← 新增方法
```

### 新增 / 修改清單

| 層級 | 檔案 | 動作 |
|------|------|------|
| Application | `Common/Interfaces/IFlexMessageBuilder.cs` | 新增介面 |
| Application | `Services/FlexMessageBuilder.cs` | 新增實作 |
| Domain | `Abstractions/ILineReplyService.cs` | 擴充 `ReplyFlexAsync` |
| Infrastructure | `Line/LineReplyService.cs` | 實作 `ReplyFlexAsync` |
| Application | `ProcessTextMessageHandler.cs` | 整合 Flex 替換 Text |
| Infrastructure | `DependencyInjection.cs` | 註冊 `IFlexMessageBuilder` |

---

## 3. Domain / Application 契約

### 3.1 IFlexMessageBuilder

```csharp
namespace VeggieAlly.Application.Common.Interfaces;

public interface IFlexMessageBuilder
{
    /// <summary>
    /// 根據驗證後的品項清單組裝 LINE Flex Message JSON (BubbleContainer)。
    /// 回傳 JsonElement 代表完整 Flex Message contents。
    /// </summary>
    object BuildBubble(IReadOnlyList<ValidatedVegetableItem> items);
}
```

### 3.2 ILineReplyService 擴充

```csharp
Task ReplyFlexAsync(string replyToken, string altText, object flexContent, CancellationToken ct = default);
```

- `altText`：純文字降級摘要（在舊版 LINE / 推播通知上顯示）
- `flexContent`：已組裝好的 Flex Message JSON contents（BubbleContainer dict）

---

## 4. Flex Message 結構規格

遵循 LINE Flex Message v2 API：

```
BubbleContainer
  ├── header: Box(layout=vertical)
  │     └── Text "📋 今日報價確認"  weight=bold  size=lg
  ├── body: Box(layout=vertical)
  │   ├── IF 有 🟢 品項：
  │   │   ├── Text "🟢 準備發布" color=#1DB446 weight=bold
  │   │   └── Separator
  │   │   └── 每個 OK 品項 → Box(layout=horizontal)
  │   │         ├── Text "{Name}"  flex=3
  │   │         └── Text "進${BuyPrice} 售${SellPrice} x{Qty}{Unit}"  flex=5  align=end
  │   ├── IF 有 🔴 品項：
  │   │   ├── Separator(margin=lg)
  │   │   ├── Text "🔴 異常待處理" color=#FF0000 weight=bold
  │   │   └── Separator
  │   │   └── 每個 Anomaly 品項 → Box(layout=vertical)
  │   │         ├── Box(layout=horizontal)
  │   │         │   ├── Text "{Name}"  flex=3
  │   │         │   └── Text "進${BuyPrice} 售${SellPrice} x{Qty}{Unit}"  flex=5  align=end
  │   │         └── Text "⚠️ {ValidationMessage}" size=xs color=#999999
  │   └── IF 全部 OK：
  │       └── (no anomaly section)
  └── footer: Box(layout=vertical) — 預留 Phase 3 一鍵發布按鈕
        └── Text "💡 如需修正，請重新傳送語音或文字" size=xs color=#AAAAAA align=center
```

---

## 5. 實作規則

1. `FlexMessageBuilder.BuildBubble()` 用 `Dictionary<string, object>` 組裝 JSON 結構，不引入第三方 Flex SDK。
2. `ReplyFlexAsync` 組裝 LINE Reply API payload：`{ type: "flex", altText, contents: {…} }`
3. Handler 改為呼叫 `ReplyFlexAsync`；若 JSON builder 失敗，fallback 回 `ReplyTextAsync` + 舊文字格式。
4. altText 格式：`"📋 報價驗證結果：{okCount}項正常, {anomalyCount}項異常"`

---

## 6. QA/QC 驗收矩陣

### 6.1 FlexMessageBuilderTests（新增）

| # | 測試 | 預期 |
|---|------|------|
| 1 | 全部 OK（2 品項） | header + 🟢 section 有 2 行，無 🔴 section |
| 2 | 全部 Anomaly（2 品項） | 有 🔴 section + warning 文字，無 🟢 section |
| 3 | 混合（1 OK + 1 Anomaly） | 兩區段皆出現 |
| 4 | 單一品項 OK | 結構完整、footer 存在 |
| 5 | 單一品項 Anomaly | 結構完整、warning 文字正確 |
| 6 | 空清單 | 拋 ArgumentException 或回傳 fallback |
| 7 | JSON 可被 JsonSerializer 序列化 | 驗證產出可 round-trip |

### 6.2 LineReplyService.ReplyFlexAsync（新增）

| # | 測試 | 預期 |
|---|------|------|
| 1 | 正常 Flex payload 送出 | POST body 包含 `type: "flex"` |
| 2 | 空 replyToken | 不發送，log warning |

### 6.3 ProcessTextMessageHandler 整合更新

| # | 測試 | 預期 |
|---|------|------|
| 1 | 正常流程 | 呼叫 `ReplyFlexAsync`（非 ReplyTextAsync） |
| 2 | Builder 拋例外 | fallback 到 ReplyTextAsync + 純文字格式 |

### 6.4 E2E Curl 驗收

| # | 向量 | 預期 |
|---|------|------|
| E1 | 高麗菜進價25賣35五十箱 | HTTP 200，WebAPI 日誌顯示 Flex Reply |
| E2 | 高麗菜進價50賣40三十箱 | HTTP 200，Anomaly section |
| E3 | 多品混合 | HTTP 200 |
