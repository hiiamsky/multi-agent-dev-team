# Code Review Report — VeggieAlly

**日期**: 2026-04-08  
**分支**: review/code-review-P3  
**審查範圍**: 整個 codebase  
**審查工具**: Claude Code (claude-sonnet-4-6)

---

## 總體評估

**結論：需修改後才可上線**

整體架構設計良好：Clean Architecture 分層正確、CQRS via MediatR 一致應用、Redis/in-memory dual store 抽象合理。但存在兩個 Critical 問題（race condition + 憑證風險）及多個 High 問題，必須在 production 部署前解決。

---

## 亮點

- Clean Architecture 分層正確，Domain 無外向依賴
- LINE HMAC-SHA256 signature 使用 `CryptographicOperations.FixedTimeEquals`，正確防禦 timing attack
- Redis draft store 有對稱的 in-memory fallback
- Dapper 全程使用參數化查詢，無 SQL injection 風險
- Cache 失敗被明確吸收，不中斷主流程
- `Publish_DbFails_DoesNotDeleteDraft` 測試是很好的失敗順序驗證範例
- Vue 前端錯誤頁面使用 DOM API 而非 `innerHTML`，安全

---

## Critical 問題

### C-1: Publish race condition — 並發請求可插入重複菜單

**位置**:
- `VeggieAlly/src/VeggieAlly.Application/Menu/Publish/PublishMenuHandler.cs:33-35`
- `VeggieAlly/src/VeggieAlly.Infrastructure/Persistence/PublishedMenuRepository.cs:78-95`

**說明**:  
Publish handler 先查 Redis cache 確認是否已發布，再插入 PostgreSQL。兩個並發請求可同時通過 `ExistsAsync` 檢查，在對方寫入 DB 前各自插入，造成重複菜單或 constraint violation。Redis 檢查不是 atomic distributed lock，不能作為唯一防線。

**修法**:

```sql
-- migration
ALTER TABLE published_menus ADD CONSTRAINT uq_published_menus_tenant_date
  UNIQUE (tenant_id, date);
```

```csharp
// PublishedMenuRepository.InsertAsync — 捕捉 unique constraint violation
catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
{
    throw new MenuAlreadyPublishedException();
}
```

---

### C-2: `.env` 檔案可能已被 commit 進 git history

**位置**: `VeggieAlly/liff-app/.env`

**說明**:  
`liff-app/.gitignore` 有排除 `.env`，但檔案存在於工作樹，可能在 `.gitignore` 加入前已被追蹤。目前值為 `VITE_LIFF_ID=test-liff-id`，雖為測試值，但此模式危險：若真實憑證曾被 commit，即使刪除檔案仍留在 git history 中。

**立即行動**:
```bash
git log --all -- VeggieAlly/liff-app/.env
```
若有記錄，需輪換憑證並使用 `git filter-repo` 或 BFG 清除 history。

---

## High 問題

### H-1: MenuController 從 header 讀取 TenantId/UserId，可被任意偽造

**位置**: `VeggieAlly/src/VeggieAlly.WebAPI/Controllers/MenuController.cs:30-32`

**說明**:  
`LiffAuthFilter` 已驗證 LINE access token 並將 `LineUserId`/`TenantId` 寫入 `HttpContext.Items`，但 `MenuController` 忽略這些已驗證的值，改從呼叫方可自由設定的 header 讀取。攻擊者只需持有任意有效 LINE token，即可偽造任何 `tenantId` 或 `userId`。`UnpublishMenu`（line 80）及 `GetTodayMenu`（line 103）有同樣問題。

**修法**:
```csharp
// 改為從 HttpContext.Items 讀取（與 DraftController 相同做法）
var tenantId = HttpContext.Items["TenantId"]?.ToString();
var userId = HttpContext.Items["LineUserId"]?.ToString();
```

---

### H-2: InventoryController 同樣信任 header 且無扣減上限

**位置**: `VeggieAlly/src/VeggieAlly.WebAPI/Controllers/InventoryController.cs:32`

**說明**:  
與 H-1 相同根本原因：`tenantId` 從可偽造的 header 取得。此外，沒有 per-user 或 per-session 的扣減上限，單一已驗證用戶可在一個 session 內扣光所有品項庫存。

**修法**: 同 H-1，從 `HttpContext.Items` 讀取；另加 application-level 扣減上限。

---

### H-3: DeductInventoryHandler 使用 First() 可能拋出未處理例外

**位置**: `VeggieAlly/src/VeggieAlly.Application/Menu/DeductInventory/DeductInventoryHandler.cs:56`

**說明**:  
```csharp
var updatedItem = updatedMenu.Items.First(i => i.Id == request.ItemId);
```
若 SQL 成功扣減但後續 re-fetch 找不到該品項（並發刪除或 tenant mismatch），會拋出 unhandled `InvalidOperationException`，回傳 500。

**修法**:
```csharp
var updatedItem = updatedMenu.Items.FirstOrDefault(i => i.Id == request.ItemId)
    ?? throw new ArgumentException($"Item {request.ItemId} not found after deduction");
```

---

### H-4: LineTokenService 未驗證 client_id，任意 LINE channel 的 token 均可通過

**位置**: `VeggieAlly/src/VeggieAlly.Infrastructure/Line/LineTokenService.cs:36-42`

**說明**:  
LINE token 驗證回應包含 `client_id`（發行該 token 的 channel）。服務只檢查 `expires_in > 0`，未確認 `client_id` 符合本應用的 Channel ID。任何持有其他 LINE 應用有效 token 的使用者均可通過驗證。

**修法**:
```csharp
if (verifyResult?.ClientId != _lineOptions.ChannelId)
{
    _logger.LogWarning("Token client_id mismatch");
    return null;
}
```
`LineOptions` 需新增 `ChannelId` 屬性並從設定讀取。

---

### H-5: LLM 回傳內容在解析失敗時完整寫入 Error log

**位置**: `VeggieAlly/src/VeggieAlly.Application/Services/ValidationReplyService.cs:148`

**說明**:  
```csharp
_logger.LogError(ex, "JSON 反序列化失敗: {Json}", jsonContent);
```
`jsonContent` 是 LLM 產生的文字，可能含有用戶提供的菜品名稱、價格等商業敏感資料，直接推送至外部 log 聚合器有資料洩漏風險。

**修法**: 僅記錄長度或截斷前 N 字元。

---

### H-6: 音訊內容全部讀進 memory，高並發下可能 OOM

**位置**: `VeggieAlly/src/VeggieAlly.Infrastructure/Line/LineContentService.cs:28`

**說明**:  
```csharp
return await response.Content.ReadAsByteArrayAsync(ct);
```
LINE 音訊檔案可達數 MB。`ReadAsByteArrayAsync` 整個緩衝在 memory，並發請求下可能導致 OOM。

**修法**: 讀取前先檢查 `Content-Length` header 並設定上限；考慮使用 streaming 而非完整緩衝。

---

## Medium 問題

### M-1: 時區不一致 — DraftMenuService 用 UTC+8，PublishMenuHandler 用 server local time

**位置**:
- `DraftMenuService.cs:36`: `TimeProvider.System.GetUtcNow().AddHours(8)` ✓
- `PublishMenuHandler.cs:30`: `DateOnly.FromDateTime(DateTime.Today)` ✗
- `DeductInventoryHandler.cs:28`: 同上 ✗
- `GetTodayMenuHandler.cs:23`: 同上 ✗

**說明**:  
若伺服器部署在 UTC（標準雲端環境），`DateTime.Today` 回傳 UTC 日期，每天有 8 小時與台灣時間不同。23:30 台灣時間建立的草稿（日期 `2026-04-08`），Publish 時會找 `2026-04-07` 的草稿而失敗。

**修法**: 建立 `ITaiwanDateService` 抽象或統一使用 `TimeProvider` + UTC+8 offset。

---

### M-2: InMemoryDraftSessionStore 無過期清除，長時間運行無限增長

**位置**: `VeggieAlly/src/VeggieAlly.Infrastructure/DependencyInjection.cs:63`

**說明**:  
`ConcurrentDictionary` 只在讀取時檢查過期，不主動清除。長時間運行下 dictionary 無限增長。

**修法**: 加入 `IHostedService` 定期清除過期 session。

---

### M-3: RedisDraftSessionStore 忽略 CancellationToken

**位置**: `VeggieAlly/src/VeggieAlly.Infrastructure/Storage/RedisDraftSessionStore.cs:31,58,72`

**說明**:  
三個 async 操作均忽略傳入的 `ct` 參數。HTTP 請求取消時，Redis 操作仍會等到 `AsyncTimeout`（設定 5 秒）才結束。

---

### M-4: PublishedMenuRepository 逐筆 INSERT 品項，N+1 問題

**位置**: `VeggieAlly/src/VeggieAlly.Infrastructure/Persistence/PublishedMenuRepository.cs:97-113`

**說明**:  
一個含 30 個品項的菜單會在一個 transaction 內發出 31 個 DB round-trip。

**修法**: 使用批次 INSERT 或 Dapper 的多筆語法。

---

### M-5: TodayMenuPage.vue hardcode tenantId，實際部署完全無法運作

**位置**: `VeggieAlly/liff-app/src/pages/TodayMenuPage.vue:113`

**說明**:  
```typescript
const tenantId = 'default-tenant-id'
```
這是已知的 placeholder，但意味著頁面目前完全無法用於真實部署。

**修法**: 從 `VITE_TENANT_ID` 環境變數、LIFF URL path 或後端回應取得。

---

### M-6: NumPadPage.vue 顯示 hardcode 假價格

**位置**: `VeggieAlly/liff-app/src/pages/NumPadPage.vue:128-133`

**說明**:  
目前無 `GET /api/draft/item/{id}` API，頁面顯示 hardcode 的買入 25 / 賣出 35。用戶看到的是假資料。

**修法**: 新增 `GET /api/draft/item/{id}` endpoint，或在 Flex Message button URL 的 query string 帶入品項名稱與價格。

---

### M-7: NumPadPage.vue 允許前導零輸入（如 "01"）

**位置**: `VeggieAlly/liff-app/src/pages/NumPadPage.vue:142-156`

**說明**:  
連按 "0" 再按 "1" 會顯示 `$01`，雖然 `parseInt` 正確解析，但顯示令人困惑。

**修法**:
```typescript
if (current === '0' && digit !== '00') {
  inputValue.value = digit
  return
}
```

---

### M-8: ValidationReplyService 吸收所有 LINE reply 失敗，用戶無反饋

**位置**: `VeggieAlly/src/VeggieAlly.Application/Services/ValidationReplyService.cs:110-114`

**說明**:  
LINE reply 失敗時草稿已建立，但用戶沒有任何提示。三層 try/catch 結構也應重構為明確的 fallback chain。

---

### M-9: Gemini 錯誤 log 不分原因，一律標示為「Gemini API 呼叫失敗」

**位置**:
- `ProcessTextMessageHandler.cs:74`
- `ProcessAudioMessageHandler.cs:100`

**說明**:  
catch block 捕捉所有 `Exception`，包含 cancellation token 和下游服務失敗，全部 log 為 Gemini 錯誤，使 ops debugging 困難。

---

### M-10: PriceValidationService — sellPrice == 0 被誤判為 Anomaly

**位置**: `VeggieAlly/src/VeggieAlly.Application/Services/PriceValidationService.cs:17-19`

**說明**:  
LLM prompt 規定用戶未提供售價時 `sell_price` 預設為 0。`sellPrice = 0` 時，`0 <= buyPrice` 永遠成立，每個沒有提供售價的品項都會被標為「售價低於或等於進價」異常，造成大量假警告。

**修法**:
```csharp
if (sellPrice > 0 && sellPrice <= buyPrice)
{
    return ValidationResult.Anomaly("售價低於或等於進價");
}
```

---

## Low 問題

| # | 位置 | 說明 |
|---|------|------|
| L-1 | `DraftController.cs:8,27` | WebAPI 層直接 import `VeggieAlly.Infrastructure.Line`，違反 Clean Architecture |
| L-2 | `DraftController.cs:137-194`、`InventoryController.cs:80-88` | DTO 定義在 controller 檔案內，應移至 `Models/` 或 `Contracts/` |
| L-3 | `FlexMessageBuilder.cs` | 全用 `Dictionary<string, object>`，無 compile-time type safety，key typo 無法偵測 |
| L-4 | `useLiff.ts:15` | `liffInstance` 是 module-level singleton，在 Vitest 跨測試污染 |
| L-5 | `TodayMenuPage.vue:193-207` | `handleSubmitOrder` 第一個失敗即中止，先前已扣減的庫存無補償機制 |
| L-6 | `ValidationReplyService.cs:174` | `StripMarkdownCodeFence` 標為 `internal static`，測試應透過 public 介面覆蓋 |
| L-7 | `LineSignatureAuthFilter.cs` | `ChannelSecret` 以明文字串傳遞，考慮使用 `ISecretManager` 避免意外 log |
| L-8 | `Program.cs:16` | OpenAPI 文件未描述 `Authorization: Bearer` 及 `X-Line-Signature` header 要求 |
| L-9 | `PublishedMenuCache.cs:73` | `TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")` 在缺少 `tzdata` 的 Linux 上會 throw |
| L-10 | `ValidationResult.cs`、`DraftController.cs:101` | `ValidationStatus` enum 在 Redis 中序列化為整數，應加 `[JsonConverter(typeof(JsonStringEnumConverter))]` |

---

## 安全性摘要

| 優先級 | 問題 | 影響 |
|--------|------|------|
| Critical | C-2 `.env` in git history | 憑證洩漏 |
| High | H-4 token audience 未驗證 | 任意 LINE channel token 可登入 |
| High | H-1/H-2 header 身份偽造 | tenant 越權操作 |
| High | H-5 LLM output 寫入 error log | 商業資料洩漏 |
| Medium | 無 rate limiting | Gemini API 配額耗盡、庫存被清空 |

---

## 測試缺口

1. `PublishMenuHandler` 無測試 — race condition 路徑（cache false 但 DB 已有資料）
2. `LineTokenService` 無測試 — `client_id` 不符時應被拒絕（因為驗證邏輯目前不存在）
3. `DraftController.CorrectItemPrice` — `HttpContext.Items` 缺少 auth key 時的 `Unauthorized` 回應
4. 無端到端整合測試：publish → deduct → get-today 完整流程
5. `PriceValidationService` 缺少 `sellPrice == 0` 的測試案例（M-10 bug 即可被此測試抓到）
6. `LineSignatureAuthFilter` 缺少空 body 的測試（LINE webhook 驗證事件傳空 body）

---

## 修復優先順序

| 優先 | 項目 | 類型 |
|------|------|------|
| 1 | **C-1** DB 加 unique constraint，防 publish race condition | 安全/正確性 |
| 2 | **C-2** 確認 `.env` 未進入 git history，必要時輪換憑證 | 安全 |
| 3 | **H-1/H-2** MenuController/InventoryController 改從 `HttpContext.Items` 取身份 | 安全 |
| 4 | **H-4** LineTokenService 驗證 `client_id` | 安全 |
| 5 | **M-10** `sellPrice == 0` 不應視為 anomaly | 用戶體驗 |
| 6 | **M-1** 統一所有時區處理為 UTC+8 | 正確性 |
| 7 | **M-5** 前端 `tenantId` 改從環境變數或 URL 取得 | 功能 |
| 8 | 加入 rate limiting middleware | 安全 |
| 9 | **M-6** 實作 `GET /api/draft/item/{id}` 或透過 URL 傳遞品項資料 | 功能 |
| 10 | **L-10** `ValidationStatus` 加 `JsonStringEnumConverter` | 可靠性 |
