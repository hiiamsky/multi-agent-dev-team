# P1-002/P1-003 增量分析報告

> SA/SD Agent 產出 | 基於 P1-001 骨架已合併到 main 的增量差異分析

---

## 一、P1-001 覆蓋率分析

### P1-002：LINE Webhook 接收 + 簽章驗證

| Issue 任務 | P1-001 已實作 | 增量需求 |
|------------|:------------:|---------|
| `[POST] /api/webhook` 端點 | ✅ WebhookController.cs | 無 |
| HMAC-SHA256 簽章驗證 | ✅ LineSignatureValidator.cs（含常數時間比對） | 無 |
| `IAsyncActionFilter` + `EnableBuffering` + Body 歸位 | ✅ LineSignatureAuthFilter.cs | 無 |
| 事件過濾（僅 `type=message` + `message.type=text`） | ✅ Controller L36-L42 | 無 |
| 空 Events / 無 ReplyToken 跳過 | ✅ Controller L29-L48 | 無 |
| Handler 例外不影響 200 OK 回傳 | ✅ Controller L52-L57 | 無 |
| Domain 模型（`LineWebhookPayload`、`LineEvent` 等） | ✅ `Domain/Models/Line/` 全部到位 | 無 |
| `ILineReplyService` 介面 + `LineReplyService` 實作 | ✅ 含 5000 字元截斷 | 無 |
| DI 配線（`LineOptions`、HttpClient） | ✅ DependencyInjection.cs | 無 |
| **單元測試** | ❌ 僅有 placeholder | **需新增** |

**P1-002 結論：SA/SD + Backend PG 任務已 100% 被 P1-001 涵蓋，僅需補充單元測試。**

---

### P1-003：MEAI 串接 Gemini API（文字 → JSON）

| Issue 任務 | P1-001 已實作 | 增量需求 |
|------------|:------------:|---------|
| `IChatClient` DI 配線 + `GeminiChatClientFactory` | ✅ GeminiChatClientFactory.cs | 無 |
| `ProcessTextMessageCommand` + `Handler` | ✅ ProcessTextMessageHandler.cs | 無 |
| ChatMessage 組裝（System + User） | ✅ Handler L49-L53 | 無 |
| JSON 驗證（`JsonDocument.Parse`） | ✅ Handler L57-L63 + `IsValidJson()` | 無 |
| Gemini 失敗 → 使用者友善錯誤訊息 | ✅ Handler L68-L72 | 無 |
| LINE Reply 獨立 try-catch | ✅ Handler L76-L83 | 無 |
| System Prompt 品項清單 | ⚠️ 基礎版（50 品項，無子品種） | **需擴充** |
| System Prompt 口語解析能力 | ⚠️ 缺乏 few-shot 範例 | **需補強** |
| `ParsedMenuItem` / `ParseResult` Domain 模型 | ✅ 已定義 | 無 |
| **單元測試** | ❌ 僅有 placeholder | **需新增** |

**P1-003 結論：核心管線已完成，但 System Prompt 品質不足以達到 PRD 驗收標準，需增量調優。**

---

## 二、P1-002 增量規格

**已由 P1-001 完成，僅需 QA/QC 驗證 + 單元測試（見第四節）。**

無額外 SA/SD 或 Backend PG 增量。

---

## 三、P1-003 增量規格：System Prompt 調優

### 3.1 問題分析

PRD 驗收標準：「傳文字給 LINE Bot → 收到結構化 JSON 回應」，且 PRD Section 3.1 明確要求解析**口語化報價**。

目前 `SystemPrompts.cs` 存在三個缺陷：

| # | 缺陷 | 影響 |
|---|------|------|
| 1 | **品項清單與 PRD 不一致**：PRD Section 6 使用子品種（初秋高麗菜、改良高麗菜、本地洋蔥、進口洋蔥、紅心地瓜、黃心地瓜、北蔥、粉蔥、老薑、嫩薑、白玉苦瓜、綠苦瓜、生香菇、甜玉米、雙色玉米、大蔥），目前 Prompt 僅有簡化名（高麗菜、洋蔥、地瓜、蔥、薑、苦瓜、香菇、玉米） | 子品種輸入無法正確匹配 |
| 2 | **缺乏口語化解析指引**：使用者會輸入「高麗菜 25 賣 35 五十箱」，Prompt 未定義「賣」= `sell_price`、中文數字對應規則 | Gemini 可能亂猜欄位對應 |
| 3 | **缺乏 few-shot 範例**：無輸入→輸出範例，LLM 缺乏錨定參考 | 輸出格式不穩定 |

### 3.2 增量規格：`SystemPrompts.VegetableParser` 修訂

**修訂範圍**：僅修改 `SystemPrompts.cs`，不動任何其他檔案。

#### 3.2.1 品項清單更新

替換為 PRD Section 6 完整清單（含子品種）：

```
品項清單：
- 葉菜類：初秋高麗菜、改良高麗菜、包心大白菜、小白菜、青江菜、空心菜、油菜、莧菜、菠菜、A菜、萵苣
- 根莖類：白蘿蔔、紅蘿蔔、馬鈴薯、本地洋蔥、進口洋蔥、紅心地瓜、黃心地瓜、芋頭、北蔥、粉蔥、蒜頭、老薑、嫩薑
- 果菜類：牛番茄、聖女番茄、小黃瓜、胡瓜、絲瓜、白玉苦瓜、綠苦瓜、茄子、彩椒、青椒、敏豆、四季豆
- 花果其他：青花菜、花椰菜、甜玉米、雙色玉米、玉米筍、豌豆、秋葵、南瓜、冬瓜、蘆筍、九層塔
- 蕈菇類：金針菇、杏鮑菇、生香菇、秀珍菇、鴻喜菇、黑木耳、白木耳
- 辛香料：香菜、芹菜、辣椒、大蔥
```

#### 3.2.2 新增口語解析規則

```
4. 若使用者只說品類名稱（如「高麗菜」）而清單有多個子品種，預設對應最常見品種，並將 is_new 設為 false
5. 中文數字轉換：「五十」→50、「一百二」→120、「二十五」→25
6. 「賣」「售」後面的數字 = sell_price，「進」「買」「成本」後面的數字 = buy_price
7. 若只有一個價格且無「賣」「售」關鍵字，視為 buy_price，sell_price 設為 0
8. 支援多品項連續輸入，每個品項自動斷句
```

#### 3.2.3 新增 few-shot 範例

```
範例 1：
輸入：「高麗菜 25 賣 35 五十箱 小白菜 15 三十箱」
輸出：
{
  "items": [
    { "name": "初秋高麗菜", "is_new": false, "buy_price": 25, "sell_price": 35, "quantity": 50, "unit": "箱" },
    { "name": "小白菜", "is_new": false, "buy_price": 15, "sell_price": 0, "quantity": 30, "unit": "箱" }
  ]
}

範例 2：
輸入：「紅蘿蔔 進18 售30 一百箱 有機菠菜 22 四十箱」
輸出：
{
  "items": [
    { "name": "紅蘿蔔", "is_new": false, "buy_price": 18, "sell_price": 30, "quantity": 100, "unit": "箱" },
    { "name": "有機菠菜", "is_new": true, "buy_price": 22, "sell_price": 0, "quantity": 40, "unit": "箱" }
  ]
}
```

### 3.3 Token 預估

| 項目 | 修訂前 | 修訂後 |
|------|--------|--------|
| Prompt 字元數（約） | ~450 | ~1200 |
| Gemini 2.0 Flash 計費影響 | - | 微乎其微，可接受 |

---

## 四、單元測試規格

測試專案：`VeggieAlly.Application.Tests`

需額外引用：`VeggieAlly.Infrastructure`（用於 `LineSignatureValidator` 測試）、`VeggieAlly.WebAPI`（用於 `WebhookController` 測試）。

### 4.1 P1-002 測試案例

**測試類別：`LineSignatureValidatorTests`**

| # | 測試方法名 | 輸入 | 預期結果 |
|---|-----------|------|---------|
| 1 | `Validate_ValidSignature_ReturnsTrue` | 已知 secret + body + 正確 HMAC-SHA256 Base64 | `true` |
| 2 | `Validate_InvalidSignature_ReturnsFalse` | 正確 secret + body + 錯誤簽章 | `false` |
| 3 | `Validate_EmptyBody_ReturnsFalse` | 任意 secret + `byte[0]` + 任意簽章 | `false` |
| 4 | `Validate_NullOrEmptySecret_ReturnsFalse` | `""` / `null` + 任意 body + 任意簽章 | `false` |
| 5 | `Validate_NullOrEmptySignature_ReturnsFalse` | 任意 secret + 任意 body + `""` / `null` | `false` |

**測試類別：`WebhookControllerTests`**（Mock `IMediator`、`ILogger`）

| # | 測試方法名 | 輸入 | 預期結果 |
|---|-----------|------|---------|
| 6 | `Receive_EmptyEvents_Returns200` | `{ "events": [] }` | 200 OK，Send 未呼叫 |
| 7 | `Receive_NullPayload_Returns200` | `null` | 200 OK |
| 8 | `Receive_TextMessage_DispatchesCommand` | text message event | Send 呼叫 1 次 |
| 9 | `Receive_NonTextMessage_SkipsEvent` | `message.type=image` | Send 未呼叫 |
| 10 | `Receive_NoReplyToken_SkipsEvent` | replyToken = null | Send 未呼叫 |
| 11 | `Receive_HandlerThrows_StillReturns200` | Send 拋 Exception | 200 OK |

### 4.2 P1-003 測試案例

**測試類別：`ProcessTextMessageHandlerTests`**（Mock `IChatClient`、`ILineReplyService`、`ILogger`）

| # | 測試方法名 | Mock 行為 | 預期結果 |
|---|-----------|----------|---------|
| 12 | `Handle_ValidText_CallsGeminiAndRepliesJson` | IChatClient 回傳合法 JSON | ReplyTextAsync 被呼叫，text = JSON |
| 13 | `Handle_GeminiReturnsNonJson_RepliesErrorMessage` | 回傳 `"I don't understand"` | Reply = `"解析失敗，請重新輸入"` |
| 14 | `Handle_GeminiReturnsEmpty_RepliesErrorMessage` | 回傳 `""` | Reply = `"解析失敗，請重新輸入"` |
| 15 | `Handle_GeminiThrowsException_RepliesSystemBusy` | 拋 HttpRequestException | Reply = `"系統忙碌中，請稍後重試"` |
| 16 | `Handle_EmptyText_SkipsProcessing` | - | IChatClient 未呼叫 |
| 17 | `Handle_NullReplyToken_SkipsProcessing` | - | IChatClient 未呼叫 |
| 18 | `Handle_LineReplyFails_DoesNotThrow` | ReplyTextAsync 拋 HttpRequestException | Handler 正常完成 |

**測試類別：`SystemPromptsTests`**（純靜態驗證）

| # | 測試方法名 | 驗證內容 |
|---|-----------|---------|
| 19 | `VegetableParser_ContainsRequiredJsonSchema` | Prompt 包含 items、name、is_new、buy_price、sell_price、quantity、unit |
| 20 | `VegetableParser_ContainsAllCategoryItems` | 包含各分類子品種關鍵字 |
| 21 | `VegetableParser_ContainsFewShotExamples` | 包含「範例」+ JSON 輸出示例 |

---

## 五、建議工作方式

| 項目 | 建議 |
|------|------|
| **分支策略** | 單一分支 `feature/2-3-incremental` 完成所有增量 |
| **工作順序** | 1️⃣ SystemPrompts.cs → 2️⃣ 單元測試 → 3️⃣ build + test → 4️⃣ PR |
| **Backend PG 交付** | 修改 1 檔 + 新增 4 測試類別（~21 方法） |
| **QA/QC 驗證** | 單元測試 + 真實 LINE Bot 手動驗收 |
