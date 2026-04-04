# P2-001: 導入價格驗證防呆機制與確切商業規則藍圖

## 1. 需求背景 (Context)
在 Phase 1 中，我們已成功透過 MEAI (`IChatClient`) 解析文字並獲取 JSON 結構。
在 Phase 2 的第一階段 ([P2-001])，我們需針對解析出來的資料，導入「**確切的商業規則 (Deterministic Rules)**」，落實「**價格分析與防呆交由 C# 運算，而非 LLM**」的架構原則。

---

## 2. 領域模型設計 (Domain Models)

建立於 `VeggieAlly.Domain/Entities/` 或 `VeggieAlly.Domain/ValueObjects/`：

```csharp
// 1. 驗證狀態列舉
public enum ValidationStatus
{
    Ok,         // 準備發布區
    Anomaly,    // 異常待處理區
    Error       // 結構錯誤或無效資料
}

// 2. 驗證結果 Value Object
public record ValidationResult(ValidationStatus Status, string? Message)
{
    public static ValidationResult Ok() => new(ValidationStatus.Ok, null);
    public static ValidationResult Anomaly(string message) => new(ValidationStatus.Anomaly, message);
    public static ValidationResult Error(string message) => new(ValidationStatus.Error, message);
}

// 3. 經過驗證的單一品項
public class ValidatedVegetableItem // 或 Record
{
    public string Name { get; init; }
    public bool IsNew { get; init; }
    public decimal BuyPrice { get; init; }
    public decimal SellPrice { get; init; }
    public int Quantity { get; init; }
    public string Unit { get; init; }
    
    // 驗證相關
    public decimal? HistoricalAvgPrice { get; init; }
    public ValidationResult Validation { get; init; }
}
```

---

## 3. 應用層介面設計 (Application Interfaces)

建立於 `VeggieAlly.Application/Common/Interfaces/` 或對應的 Services 資料夾中：

### 3.1 歷史價格查詢介面 (IVegetablePricingService)
*註：在 [P2-001] 由於尚未實作真實 Database 連線，請在 Infrastructure 層實作一個 `MockVegetablePricingService`，隨機或依固定 Dictionary 回傳歷史均價。*

```csharp
public interface IVegetablePricingService
{
    /// <summary>
    /// 查詢過去 N 日同品項平均進價
    /// </summary>
    Task<decimal?> GetHistoricalAvgPriceAsync(string itemName, int days = 7, CancellationToken cancellationToken = default);
}
```

### 3.2 價格防呆驗證器 (IPriceValidationService)
*註：雖然 PRD 提到 SK Plugin，但由於目前架構由 Webhook Handler 取得 JSON 後直接進行 C# 邏輯驗證，我們設計為標準的 Application Service 即可，未來若需要給 Agent Planner 呼叫亦可輕鬆標上 `[KernelFunction]`。*

```csharp
public interface IPriceValidationService
{
    /// <summary>
    /// 執行商業防呆規則
    /// </summary>
    ValidationResult Validate(decimal buyPrice, decimal sellPrice, decimal? historicalAvgPrice);
}
```

---

## 4. 實作細節與防呆規則 (Implementation Rules)

針對 `IPriceValidationService.Validate`，請後端 PG 實作以下規則（順序代表優先度）：

1. **賠本防呆**：
   - 條件：`sellPrice <= buyPrice`
   - 結果：`ValidationResult.Anomaly("售價低於或等於進價")`
2. **歷史波動防呆**：
   - 條件：`historicalAvgPrice.HasValue` 且 `Math.Abs(buyPrice - historicalAvgPrice.Value) / historicalAvgPrice.Value > 0.30m`
   - 結果：`ValidationResult.Anomaly($"與歷史均價落差 {deviation:P0}")`
3. **正常通過**：
   - 結果：`ValidationResult.Ok()`

---

## 5. 與現有 Handler 的整合 (Integration)

在 `ProcessTextMessageHandler.cs` 中：
1. 確保從 LLM 取得 JSON 後，正確反序列化為 `List<VegetableItem>` (原始 Model)。
2. Injection (DI) 注入 `IVegetablePricingService` 與 `IPriceValidationService`。
3. 走訪所有 `VegetableItem`：
   - 呼叫 `GetHistoricalAvgPriceAsync` 取得歷史均價。
   - 呼叫 `Validate` 取得 `ValidationResult`。
   - 組合為 `ValidatedVegetableItem` 清單。
4. 本 Sprint 暫不處理 Flex Message，所以請將驗證結果（例如有哪幾項是異常、哪幾項是正常）透過簡單的 Markdown 條列格式，傳給 `ReplyTextAsync` 回傳給使者，證明防呆機制生效。

---

## 6. QA/QC 驗收計畫（API 層 / 單元測試）

> **QA/QC 職責**：驗證所有 C# 商業邏輯的正確性、邊界條件與錯誤處理。
> 以下測試全部使用 xUnit + NSubstitute，不依賴外部服務。

### 6.1 PriceValidationService 單元測試

測試類別：`PriceValidationServiceTests.cs`

| # | 測試案例 | 輸入 (buy, sell, histAvg) | 預期結果 |
|---|---------|--------------------------|---------|
| 1 | 正常利潤 | (25, 35, 26) | `Ok` |
| 2 | 售價等於進價 | (30, 30, 28) | `Anomaly("售價低於或等於進價")` |
| 3 | 售價低於進價（賠本） | (50, 40, 48) | `Anomaly("售價低於或等於進價")` |
| 4 | 歷史波動跳升 31% | (131, 180, 100) | `Anomaly("與歷史均價落差 31%")` |
| 5 | 歷史波動暴跌 31% | (69, 90, 100) | `Anomaly("與歷史均價落差 31%")` |
| 6 | 剛好 30% 不觸發 | (130, 180, 100) | `Ok`（邊界：30% 不算異常） |
| 7 | 無歷史價格但利潤正常 | (25, 35, null) | `Ok` |
| 8 | 無歷史價格且賠本 | (50, 40, null) | `Anomaly("售價低於或等於進價")`（賠本規則優先） |
| 9 | 歷史均價為 0（防除以零） | (25, 35, 0) | `Ok`（應安全處理，不 throw） |

**關鍵斷言**：
- 規則 1（賠本）優先於規則 2（波動），即 `sellPrice <= buyPrice` 時不需再檢查波動。
- `historicalAvgPrice == 0` 時不得拋出 `DivideByZeroException`。

### 6.2 MockVegetablePricingService 單元測試

測試類別：`MockVegetablePricingServiceTests.cs`

| # | 測試案例 | 輸入 | 預期結果 |
|---|---------|------|---------|
| 1 | 已知品項查詢 | "初秋高麗菜" | 回傳 non-null decimal |
| 2 | 未知品項查詢 | "火星菜" | 回傳 `null` |
| 3 | 空白品項名稱 | "" | 回傳 `null`，不 throw |

### 6.3 ProcessTextMessageHandler 整合測試（Mock 注入）

測試類別：`ProcessTextMessageHandlerTests.cs`（擴充現有 9 個測試）

| # | 測試案例 | 模擬情境 | 預期回覆內容包含 |
|---|---------|---------|----------------|
| 10 | 單品正常通過 | LLM 回 JSON (buy=25, sell=35)，Mock 歷史價=26 | "🟢" 與品項名稱 |
| 11 | 單品賠本觸發 | LLM 回 JSON (buy=50, sell=40) | "🔴" 與 "售價低於或等於進價" |
| 12 | 單品波動觸發 | LLM 回 JSON (buy=100, sell=150)，Mock 歷史價=20 | "🔴" 與 "歷史均價落差" |
| 13 | 多品混合（正常+異常） | LLM 回 2 品項 JSON，一正常一賠本 | 回覆同時包含 "🟢" 和 "🔴" |
| 14 | 新品項（is_new=true） | LLM 回 JSON (is_new=true)，Mock 查無歷史價 | 正常通過但標示新品 |
| 15 | JSON 缺少必要欄位 | LLM 回 `{"items":[{"name":"菜"}]}` | 不 throw，回傳錯誤提示 |

### 6.4 Domain Model 測試

測試類別：`ValidationResultTests.cs`

| # | 測試案例 | 預期 |
|---|---------|------|
| 1 | `ValidationResult.Ok()` | Status=Ok, Message=null |
| 2 | `ValidationResult.Anomaly("msg")` | Status=Anomaly, Message="msg" |
| 3 | `ValidationResult.Error("err")` | Status=Error, Message="err" |

---

## 7. E2E 驗收計畫（端到端 curl 測試）

> **E2E 職責**：以真實 HTTP 請求打進 WebAPI，驗證從 Webhook 接收 → LLM 解析 → 價格驗證 → 回覆的完整鏈路。
> 使用 Ollama (gemma4:31b) 作為本地 LLM 後端，搭配 HMAC-SHA256 簽章。

### 7.1 測試環境前置條件

| 項目 | 要求 |
|------|------|
| WebAPI | `dotnet run --launch-profile http` (port 5273) |
| Ollama | gemma4:31b 已載入 (localhost:11434) |
| User Secrets | `Line:ChannelSecret=test-secret-for-local`, `Line:ChannelAccessToken=test-token-placeholder` |
| AI Provider | `appsettings.json` → `AI:Provider=ollama` |

### 7.2 E2E 測試向量

| # | 輸入文字 | 預期驗證行為 | 預期回覆包含 |
|---|---------|-------------|-------------|
| E1 | "高麗菜進價25賣35五十箱" | 正常品項 + Mock 歷史價在合理範圍 | "🟢 高麗菜" |
| E2 | "高麗菜進價50賣40三十箱" | 賠本防呆觸發 | "🔴 高麗菜" + "售價低於或等於進價" |
| E3 | "青江菜進價100賣150十箱" | 歷史波動防呆觸發（Mock 歷史價=20） | "🔴 青江菜" + "歷史均價落差" |
| E4 | "高麗菜進價25賣35五十箱 青江菜進價50賣40十箱" | 多品混合：一正常一異常 | 同時包含 "🟢" 和 "🔴" |
| E5 | "火龍果進價80賣120二十箱" | 新品項 (is_new=true)，無歷史價 | 標示為「新品項」且正常通過 |
| E6 | "空心菜進價30賣30十箱" | 售價等於進價 | "🔴 空心菜" + "售價低於或等於進價" |

### 7.3 curl 測試模板

```bash
# 設定變數
SECRET="test-secret-for-local"
BODY='{"events":[{"type":"message","replyToken":"test-reply-token","source":{"type":"user","userId":"U123"},"message":{"id":"msg1","type":"text","text":"高麗菜進價50賣40三十箱"}}]}'

# 計算 HMAC-SHA256 簽章
SIGNATURE=$(echo -n "$BODY" | openssl dgst -sha256 -hmac "$SECRET" -binary | base64)

# 發送請求
curl -s -w "\nHTTP Status: %{http_code}\n" \
  -X POST http://localhost:5273/api/webhook \
  -H "Content-Type: application/json" \
  -H "x-line-signature: $SIGNATURE" \
  -d "$BODY"
```

### 7.4 驗收通過標準 (Exit Criteria)

- [ ] 所有單元測試（§6.1–§6.4）通過，共約 24+ 個測試案例
- [ ] E2E 測試向量 E1–E6 全部回傳 HTTP 200
- [ ] WebAPI 日誌無 `NullReferenceException` 或 `DivideByZeroException`
- [ ] 回覆文字正確包含 🟢/🔴 標示與對應的異常原因
- [ ] `ProcessTextMessageHandler` 新增的驗證邏輯不破壞現有 9 個既有測試
