# OWASP API Security Top 10:2023

**權威來源**：https://owasp.org/API-Security/editions/2023/en/0x11-t10/

**版本說明**：本章節為**後端 PG 的必讀規範**。API 層的安全威脅與傳統 Web App 不同，聚焦於物件層授權、資源消耗、業務流程濫用。

2023 版相較 2019 版的主要變動：
- API3 合併 Excessive Data Exposure + Mass Assignment → **Broken Object Property Level Authorization**
- API4 改名為 **Unrestricted Resource Consumption**（不限於 Rate Limiting）
- API6 新增 **Unrestricted Access to Sensitive Business Flows**
- API7 新增 **Server-Side Request Forgery**（API 層獨立列出）
- API10 新增 **Unsafe Consumption of APIs**
- Injection 移除（已含於其他類別）

---

## API1:2023 — Broken Object Level Authorization（BOLA）

**佔所有 API 攻擊約 40%，為最常見的 API 威脅，自 2019 年起蟬聯第一。**

**威脅定義**：API 端點暴露物件識別碼（`GET /orders/{id}`、`PUT /users/{id}`），但未驗證當前使用者是否擁有該物件。

**後端 PG 實作規範**：
- 每個操作特定資源的端點必須在 Handler 中驗證所有權：
  ```csharp
  var order = await _repo.GetByIdAsync(orderId);
  if (order.CustomerId != currentUserId)
      return Forbid();
  ```
- **禁止僅靠「URL 包含我的 userId」做授權判斷**——攻擊者可直接改 URL 中的 ID
- 使用 UUID 而非自增 ID 作為對外 ID（減少枚舉風險，但不可取代授權檢查）

**QA/QC 驗證規範**：
- 測試案例必須包含「A 使用者嘗試存取 B 使用者的資源」場景
- 每個 `{id}` 路由參數的端點都必須驗證有所有權檢查

---

## API2:2023 — Broken Authentication

**威脅定義**：認證機制實作有缺陷，讓攻擊者能接管帳號。

**後端 PG 實作規範**：
- 改密、改 email、改手機等敏感操作必須驗證當前密碼或要求 MFA
- JWT 必須驗證 expiration、issuer、audience（見 Web Top 10 A07）
- Refresh Token 使用 rotation 機制，舊 token 立即失效
- **禁止在 URL query string 傳遞 Token**（會落在伺服器 log 與瀏覽器歷史）
- 登入失敗訊息統一，不洩漏帳號是否存在
- 密碼重設 Token 一次性使用，15 分鐘過期
- 登入嘗試速率限制（同 IP、同帳號雙軌）

---

## API3:2023 — Broken Object Property Level Authorization

**威脅定義**：合併原 API3:2019 Excessive Data Exposure 與 API6:2019 Mass Assignment。根因是**物件屬性層級**的授權驗證缺失。

**後端 PG 實作規範**：

**Response 端（防 Excessive Data Exposure）**：
- **Response DTO 必須明確列出要回傳的欄位**
- **禁止直接 serialize Domain Entity**：
  ```csharp
  // 禁止
  return Ok(user);

  // 允許
  return Ok(new UserResponseDto { Id = user.Id, Name = user.Name });
  ```
- 敏感欄位（`PasswordHash`、`SecurityStamp`、`internalNotes`）絕不出現於 DTO

**Request 端（防 Mass Assignment）**：
- **Request DTO 必須明確列出可接受的欄位**
- **禁止 `[FromBody] User user` 直接綁 Entity**：
  ```csharp
  // 禁止
  public async Task<IActionResult> UpdateUser([FromBody] User user) { ... }

  // 允許
  public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequestDto dto) { ... }
  ```
- 權限欄位（`role`、`isAdmin`、`permissions`）絕不出現於一般使用者的 Request DTO

**前端 PG 跨域檢視重點**：
- 驗證後端 Response 無多餘敏感欄位
- 驗證前端 Request Payload 只包含 Request DTO 定義的欄位

---

## API4:2023 — Unrestricted Resource Consumption

**威脅定義**：API 未限制資源消耗，導致 DoS、成本暴增、或被用於 brute-force。

**後端 PG 實作規範**：
- **Rate Limiting**：
  - 公開端點（登入、註冊）：依 IP 限制（建議 10 次/分鐘）
  - 認證端點：依 User 限制
  - 使用 `AspNetCoreRateLimit` 或等效套件
- **檔案上傳限制**：
  - 大小上限（具體數值依業務設計）
  - MIME type 白名單
  - 檔名清理（避免 path traversal）
- **分頁查詢強制 `pageSize` 上限**（通常 ≤ 100）
- **查詢 timeout**：
  - Dapper 查詢設定 `commandTimeout`
  - 長時間操作改為背景工作
- **外部 API 呼叫限制**：第三方付費 API（SMS、Email）必須有速率限制與成本警報

---

## API5:2023 — Broken Function Level Authorization（BFLA）

**威脅定義**：管理員功能與一般使用者功能的授權區隔失敗。

**後端 PG 實作規範**：
- 管理類端點（`/admin/*`、`/api/internal/*`）的授權檢查必須獨立於一般端點
- 使用 Role-based Authorization：
  ```csharp
  [Authorize(Roles = "Admin")]
  public class AdminController : ControllerBase { ... }
  ```
- 或使用 Policy-based Authorization（更細粒度）：
  ```csharp
  [Authorize(Policy = "RequireAdminRole")]
  ```
- 管理介面與一般介面路由前綴明確區隔，便於審計

---

## API6:2023 — Unrestricted Access to Sensitive Business Flows（2023 新增）

**威脅定義**：業務流程遭自動化濫用，即使每次請求都合法。典型案例：
- 熱門商品被機器人大量搶購
- 優惠券被自動刷取
- 商品資料被競品爬蟲大量抓取
- 新會員優惠被同人註冊多帳號濫用

**SA/SD 階段規範**：
- 在藍圖中**明確標註哪些端點屬於「敏感業務流程」**
- 為敏感業務流程設計額外防護：
  - CAPTCHA（Google reCAPTCHA v3 或 hCaptcha）
  - 裝置指紋（Device Fingerprinting）
  - 帳號信譽分（Account Reputation）
  - 行為分析（Behavioral Analysis）

**後端 PG 實作規範**：
- 依藍圖標註，對敏感端點套用對應防護
- 記錄業務流程的使用軌跡，便於偵測異常模式

---

## API7:2023 — Server-Side Request Forgery（SSRF）

**注意**：Web Top 10:2025 將 SSRF 併入 A01，但 API Security Top 10:2023 仍獨立列出，因為 API 層的 SSRF 攻擊特性不同（常見於 webhook、URL fetching、SSO integration）。

**威脅定義**：API 取得遠端資源時未驗證使用者提供的 URI，導致攻擊者能讓伺服器對任意目標發送請求。

**後端 PG 實作規範**：
- **URL 白名單驗證**：
  ```csharp
  var allowedHosts = new[] { "api.trusted-partner.com", "webhook.internal" };
  if (!allowedHosts.Contains(uri.Host))
      throw new UnauthorizedException("Host not allowed");
  ```
- **禁止請求**：
  - `localhost`、`127.0.0.1`、`::1`
  - `169.254.169.254`（AWS/GCP metadata endpoint）
  - 內網 IP 段：`10.0.0.0/8`、`172.16.0.0/12`、`192.168.0.0/16`
  - `file://`、`ftp://`、`gopher://` 協定
- **禁止跟隨重導向到內網位址**（攻擊者可用公開網址 302 重導向到內網）
- DNS 解析必須驗證（防止 DNS rebinding）

---

## API8:2023 — Security Misconfiguration

**威脅定義**：API 與其支援系統的複雜組態導致暴露。

**規範摘要**（詳見 Web Top 10 A02）：
- 禁用不必要的 HTTP 方法（如 `TRACE`）
- CORS 設定白名單，禁止 `Access-Control-Allow-Origin: *` 搭配 credentials
- 安全 HTTP Header 配置
- 關閉 Swagger UI 於生產環境（或加認證保護）

---

## API9:2023 — Improper Inventory Management

**威脅定義**：API 版本管理不當，舊版本或遺留端點成為攻擊面。

**後端 PG 實作規範**：
- 所有 API 版本必須列在 inventory（OpenAPI / Swagger spec）
- API 版本化：URL 路徑版本化（`/api/v1/`、`/api/v2/`）或 Header 版本化
- **舊版 API 必須標註 deprecation 日期與 sunset 計畫**：
  ```csharp
  [Obsolete("Use /api/v2/users instead. Sunset: 2026-06-01")]
  [HttpGet("/api/v1/users")]
  ```
- **禁止遺留「影子 API」**：
  - dev/staging 端點誤部署至生產
  - 內部工具端點暴露於網際網路
- 環境隔離：dev、staging、production 使用獨立的 API Gateway 與認證

---

## API10:2023 — Unsafe Consumption of APIs

**威脅定義**：後端呼叫第三方 API 時，把對方回傳資料視為可信。攻擊者可能攻擊你的第三方合作夥伴，間接攻擊你。

**後端 PG 實作規範**：
- **後端呼叫第三方 API 時，同樣視為不可信輸入**：
  - 驗證 Response Schema（JSON Schema 或 strongly-typed DTO）
  - 驗證資料範圍（數值上下限、字串長度、格式）
  - 驗證編碼（防 Unicode 攻擊）
- **第三方 API 呼叫設計規範**：
  - 明確的 timeout 設定（連線 5s、讀取 30s）
  - 重試策略（指數退避，非無限重試）
  - 熔斷機制（Circuit Breaker，如 Polly）
  - TLS 驗證啟用（禁止 `ServerCertificateCustomValidationCallback = (_, _, _, _) => true`）
- **禁止「對方是合作夥伴就跳過驗證」的思維**
- 敏感資料傳輸使用雙向 TLS（mTLS）

**QA/QC 驗證規範**：
- 測試第三方 API 異常回應的處理（超時、500 錯誤、malformed JSON）
- 驗證第三方 API 的憑證 pinning（若業務要求）
