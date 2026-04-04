# 菜商神隊友
## 產品需求文件｜開發團隊技術規格版

**版本**：v2.0
**日期**：2026-03-25
**技術棧**：.NET 10 / PostgreSQL / Redis / Vue 3 / LINE API

---

## 1. 系統架構總覽

```
[ 使用者層 ]
  LINE Chatbot（採購端）               LINE LIFF / Vue 3（銷售端）
         ↓  Webhook                          ↓  REST API
[ 後端層：.NET 10 + Clean Architecture + CQRS ]
  WebhookController                    LiffApiController
         ↓                                   ↓
  MediatR / 自製 Mediator（Command / Query Handler）
         ↓                    ↓
  SK Plugins                  MEAI IChatClient
  (業務邏輯)                  (Gemini API)
         ↓
[ 資料層 ]
  PostgreSQL（主庫）          Redis（當日菜單快取）
```

---

## 2. 技術棧規格

| 層次 | 選型 | 版本 / 說明 |
|------|------|------------|
| 後端框架 | ASP.NET Core Web API | .NET 10 |
| 架構模式 | Clean Architecture + CQRS | 無 Stored Procedure |
| ORM | Dapper | Repository Pattern，手寫 SQL |
| AI 呼叫抽象 | Microsoft.Extensions.AI（MEAI） | `IChatClient` 統一介面 |
| AI 工具封裝 | Semantic Kernel Plugins | 業務邏輯封裝，不讓 LLM 做決策 |
| AI 服務 | Gemini API | STT + 圖像辨識 + JSON 解析 |
| AI 備援 | OpenAI Whisper API | Gemini 故障時自動切換（MEAI 換一行） |
| 前端 | Vue 3 + LIFF SDK | 銷售端微型網頁、數字鍵盤 |
| 主資料庫 | PostgreSQL 16 | 多租戶、庫存、歷史價格 |
| 快取 | Redis 7 | 當日草稿菜單、已發布菜單 |
| 部署 | Docker + GitHub Actions | Railway / Azure Container Apps |

---

## 3. 模組規格

### 3.1 模組一：AI 語音 / 圖文輸入解析

**職責**：接收 LINE 傳來的語音或照片，解析為結構化 JSON。

**Webhook 事件處理流程**

```csharp
// WebhookController.cs
[HttpPost("webhook")]
public async Task<IActionResult> Receive([FromBody] LineWebhookPayload payload)
{
    foreach (var evt in payload.Events)
    {
        await _mediator.Send(new ProcessLineEventCommand(evt));
    }
    return Ok();
}
```

**MEAI 呼叫規格**

```csharp
// 使用 IChatClient，與底層 Provider 解耦
IChatClient chatClient = ...; // DI 注入，切換 Gemini/OpenAI 只改 DI 設定

var response = await chatClient.CompleteAsync(new ChatMessage[]
{
    new(ChatRole.System, SystemPrompts.VegetableParser),
    new(ChatRole.User, audioContent) // 語音或圖片 content
});
```

**Gemini 解析 Prompt 規格**

系統 Prompt 須包含：
1. 50+ 品項清單（見第 6 節）作為辨識錨點
2. 要求回傳嚴格 JSON 格式：
```json
{
  "items": [
    {
      "name": "高麗菜",
      "is_new": false,
      "buy_price": 25,
      "sell_price": 35,
      "quantity": 50,
      "unit": "箱"
    }
  ]
}
```
3. 遇到不在清單的品項，`is_new: true`，`name` 填入辨識到的原始名稱

**SK Plugin：VegetableDatabasePlugin**

```csharp
public class VegetableDatabasePlugin
{
    [KernelFunction]
    public async Task<string> NormalizeName(string rawName)
        // 模糊比對標準品項名稱，回傳最接近的標準名稱或標記新品

    [KernelFunction]
    public async Task<decimal> GetHistoricalAvgPrice(string itemName, int days = 7)
        // 查詢過去 N 日同品項平均進價
}
```

---

### 3.2 模組二：防呆確認卡片

**職責**：將解析結果分類，產生 LINE Flex Message JSON。

**SK Plugin：PriceValidationPlugin**

> 設計原則：價格驗證為確定性商業規則，全部由 C# 執行，不交由 LLM 決策。

```csharp
public class PriceValidationPlugin
{
    [KernelFunction]
    public ValidationResult Validate(ParsedItem item, decimal historicalAvgPrice)
    {
        if (item.SellPrice <= item.BuyPrice)
            return ValidationResult.Anomaly("售價低於進價");

        var deviation = Math.Abs(item.BuyPrice - historicalAvgPrice) / historicalAvgPrice;
        if (deviation > 0.30m)
            return ValidationResult.Anomaly($"與歷史均價落差 {deviation:P0}");

        return ValidationResult.Ok();
    }
}
```

**Flex Message 結構規格**

```
BubbleContainer
  ├── Header：今日報價確認 + 🔊 播放原音按鈕
  ├── Body
  │   ├── Section 🟢 準備發布區（正常品項列表）
  │   └── Section 🔴 異常待處理區（含「✏️ 修正」按鈕）
  └── Footer：🚀 一鍵發布（初始 hidden，異常清空後解鎖）
```

**Redis 草稿暫存規格**

- Key：`{tenant_id}:draft:{line_user_id}:{date}`
- TTL：24 小時
- 資料結構：JSON 序列化的 `DraftMenuSession`

---

### 3.3 模組三：雙軌錯誤修正

**語音覆寫流程**

```
使用者回傳語音 → Webhook 接收
→ 從 Redis 取得草稿（識別目前待修正品項）
→ MEAI 呼叫 Gemini STT 解析新語音
→ PriceValidationPlugin 重新驗證
→ 更新 Redis 草稿 → 回傳更新後的 Flex Message
```

**智慧九宮格（LIFF 數字鍵盤）規格**

- LIFF 網頁接收 querystring：`?item_id={id}&field={buy_price|sell_price}`
- 按鈕尺寸：最小 80×80px，含 `00` 快捷鍵
- 送出後呼叫後端 `PATCH /api/draft/item/{id}`
- 後端更新 Redis 草稿並回傳最新驗證狀態

**SK Plugin：PriceCorrectPlugin**

```csharp
[KernelFunction]
public async Task<DraftItem> CorrectPrice(
    string tenantId, string itemId, decimal newBuyPrice, decimal newSellPrice)
{
    // 1. 從 Redis 取草稿
    // 2. 更新指定品項價格
    // 3. 重新執行 PriceValidationPlugin.Validate()
    // 4. 寫回 Redis
    // 5. 回傳更新後的 DraftItem（含新的驗證狀態）
}
```

---

### 3.4 模組四：銷售端今日菜單

**API 規格**

```
GET  /api/menu/today?tenant_id={id}
  → 回傳當日已發布品項（含售價、庫存）

PATCH /api/menu/inventory
  Body: { item_id, amount, tenant_id }
  → 扣除庫存（含 Transaction 防超賣）
```

**庫存扣除：PostgreSQL Row-Level Lock**

```csharp
await using var tx = await conn.BeginTransactionAsync();
var affected = await conn.ExecuteAsync(
    @"UPDATE inventory
      SET qty = qty - @amount
      WHERE item_id = @itemId
        AND tenant_id = @tenantId
        AND qty >= @amount",
    new { amount, itemId, tenantId }, tx);

if (affected == 0)
    throw new InsufficientStockException(itemId);

await tx.CommitAsync();
```

**Redis 快取規格（已發布菜單）**

- Key：`{tenant_id}:menu:published:{date}`
- TTL：當日 23:59 過期
- 發布時寫入，庫存異動時即時更新（write-through）

---

## 4. 多租戶設計

| 項目 | 規格 |
|------|------|
| 隔離策略 | 所有資料表加 `tenant_id UUID NOT NULL` |
| Dapper 規則 | 所有 Query / Command 必須帶入 `tenantId`，禁止跨租戶查詢 |
| Redis Key | 前綴統一加 `{tenant_id}:` |
| LINE 帳號 | 每租戶獨立 LINE 官方帳號，1:1 綁定 `tenant_id` |

---

## 5. 最高風險假設（Phase 1 必須驗證）

| 風險 | 驗證方式 | 接受標準 |
|------|---------|---------|
| Gemini STT 對台灣腔菜名辨識率 | 用真實語音測試 30 筆，統計正確率 | ≥ 85% 才繼續 Phase 2 |
| LINE 語音格式（.m4a）是否被 Gemini 直接接受 | PoC 測試，確認不需轉檔 | 直接接受；否則加入 FFmpeg 轉換流程 |
| 批發市場 4G 訊號穩定性 | 實地測試語音上傳延遲 | P95 延遲 < 5 秒 |

---

## 6. 預設品項資料庫

| 分類 | 品項 |
|------|------|
| 葉菜類 | 初秋高麗菜、改良高麗菜、包心大白菜、小白菜、青江菜、空心菜、油菜、莧菜、菠菜、A菜、萵苣 |
| 根莖類 | 白蘿蔔、紅蘿蔔、馬鈴薯、本地洋蔥、進口洋蔥、紅心地瓜、黃心地瓜、芋頭、北蔥、粉蔥、蒜頭、老薑、嫩薑 |
| 果菜類 | 牛番茄、聖女番茄、小黃瓜、胡瓜、絲瓜、白玉苦瓜、綠苦瓜、茄子、彩椒、青椒、敏豆、四季豆 |
| 花果其他 | 青花菜、花椰菜、甜玉米、雙色玉米、玉米筍、豌豆、秋葵、南瓜、冬瓜、蘆筍、九層塔 |
| 蕈菇類 | 金針菇、杏鮑菇、生香菇、秀珍菇、鴻喜菇、黑木耳、白木耳 |
| 辛香料 | 香菜、芹菜、辣椒、大蔥 |

---

## 7. 開發路線圖

### Phase 1（Week 1–2）：基礎建設
- [ ] .NET 10 WebAPI 專案建立（Clean Architecture 資料夾結構）
- [ ] LINE Messaging API Webhook 接收 + 簽章驗證
- [ ] MEAI 串接 Gemini API（文字輸入 → JSON，先不做語音）
- [ ] Google Sheets 暫存（MVP 快速上線）
- [ ] **驗收**：傳文字給 LINE Bot → 收到結構化 JSON 回應

### Phase 2（Week 3–4）：AI 核心
- [ ] SK Plugin：VegetableDatabasePlugin
- [ ] SK Plugin：PriceValidationPlugin
- [ ] LINE Flex Message 紅綠燈卡片
- [ ] 語音（.m4a）→ Gemini STT 流程
- [ ] **驗收**：語音輸入 → 正確觸發紅綠燈卡片

### Phase 3（Week 5–6）：UX 閉環
- [ ] 雙軌修正（語音覆寫 + LIFF 數字鍵盤）
- [ ] 一鍵發布鎖定邏輯
- [ ] 銷售端 LIFF 今日菜單（Vue 3）
- [ ] 庫存扣除 API + Transaction
- [ ] **驗收**：完整流程端對端測試通過

### Phase 4（Week 7–8）：規模化
- [ ] Google Sheets → PostgreSQL 遷移
- [ ] Redis 快取整合
- [ ] 多租戶隔離實作
- [ ] **驗收**：兩租戶同時操作互不干擾，庫存無超賣

### Phase 5（Week 9+）：商業化
- [ ] Docker 容器化 + CI/CD
- [ ] 部署至 Railway / Azure Container Apps
- [ ] 計費模組 + 金流串接
