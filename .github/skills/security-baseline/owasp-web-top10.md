# OWASP Top 10:2025（Web Application）

**權威來源**：https://owasp.org/Top10/2025/

**版本說明**：2025 版相較 2021 版的主要變動：
- A03 新增 **Software Supply Chain Failures**（擴展原 A06 Vulnerable Components 至整個建置/散佈/更新生命週期）
- A10 新增 **Mishandling of Exceptional Conditions**（強調 fail-safe 文化）
- **SSRF 併入 A01 Broken Access Control**（不再獨立成項）
- A02 Security Misconfiguration 從第 5 升至第 2
- A04 Cryptographic Failures 從第 2 降至第 4

---

## A01: Broken Access Control（存取控制失效，#1）

**威脅定義**：使用者（或攻擊者）能執行超出其權限範圍的操作或存取資源。**SSRF 在 2025 版併入本類。**

**後端 PG 實作規範**：
- 每個需認證的端點必須標註 `[Authorize]` 或等效 attribute；未標註 `[AllowAnonymous]` 者一律要求認證
- 資源所有權驗證：操作涉及 `userId` 的端點，必須在 Handler 中驗證 `currentUserId == resource.OwnerId`
- 防 IDOR（Insecure Direct Object Reference）：前端傳入的 ID 必須經後端授權驗證，不得盲信
- **SSRF 防護**（2025 版新歸屬）：後端發起的 HTTP 請求必須：
  - URL 白名單驗證
  - 禁止請求 `localhost`、`127.0.0.1`、`169.254.169.254`（AWS metadata）、內網 IP 段（10.x、172.16-31.x、192.168.x）
  - 禁止跟隨重導向到內網位址

**前端 PG 實作規範**：
- 前端路由守衛僅為 UX 提示，絕非安全邊界
- 敏感操作按鈕必須依伺服器回傳的權限資料顯示/隱藏，不可由前端自行判斷

**DBA 實作規範**：
- 應用程式帳號僅授予業務資料表的必要權限
- 禁止應用程式使用 `sa` / `dbo` / 超級管理員帳號連線

---

## A02: Security Misconfiguration（安全組態錯誤，#2，從第 5 上升）

**威脅定義**：系統、應用、Cloud 服務組態錯誤導致暴露。2025 版升至第 2 名反映現代應用高度依賴組態。

**後端 PG 實作規範**：
- 禁用生產環境的 debug 端點、詳細錯誤頁面、Swagger UI（除非受認證保護）
- 強制安全 HTTP Header：
  - `Strict-Transport-Security: max-age=31536000; includeSubDomains`
  - `X-Content-Type-Options: nosniff`
  - `Content-Security-Policy`（具體策略依功能設計）
  - `X-Frame-Options: DENY` 或 CSP `frame-ancestors 'none'`
- 移除預設帳號；首次部署必須強制改密

**DBA 實作規範**：
- 禁用資料庫預設帳號（`sa`、`root` 無密碼）
- 限制資料庫監聽網卡（不要 `0.0.0.0`）

**QA/QC 驗證規範**：
- 掃描 `appsettings.Production.json` 確認無 debug 旗標殘留
- 驗證 HTTP Response Header 符合基線

---

## A03: Software Supply Chain Failures（軟體供應鏈失效，#3，2025 新增）

**威脅定義**：建置、散佈、更新過程中的第三方程式碼、工具、依賴項遭篡改或引入漏洞。涵蓋整個建置/散佈/更新生命週期，包含 CI/CD pipeline、容器倉庫、建置工具。

**全 Agent 實作規範**：
- 所有第三方套件必須有明確版本固定（lockfile 納入版控）：
  - .NET：`packages.lock.json` 強制啟用
  - Node.js：`package-lock.json` 或 `yarn.lock`
  - Python：`requirements.txt` 含 hash
- CI/CD pipeline 必須執行 SCA（Software Composition Analysis）掃描
- 使用官方來源（NuGet 官方 registry、npm 官方 registry），禁止來路不明的第三方 registry
- 禁止使用 typo-squatting 風險包（近似名稱但作者不同的套件）
- 產出 SBOM（Software Bill of Materials）以利漏洞追溯

**詳細工具鏈規範**：見 `supply-chain-tooling.md`

---

## A04: Cryptographic Failures（加密失效，#4，從第 2 下降）

**威脅定義**：缺乏加密、加密強度不足、金鑰洩漏。

**後端 PG 實作規範**：
- 密碼雜湊使用 `BCrypt.Net-Next`（cost ≥ 12）或 `Argon2`，**禁止 MD5、SHA1、無鹽 SHA256**
- 需可還原的敏感欄位使用 AES-256-GCM（優於 CBC，GCM 提供完整性驗證）
- 金鑰從設定管理取得（User Secrets、Azure Key Vault、AWS KMS），禁止硬編碼
- 傳輸層強制 TLS 1.2+，禁用 SSLv3、TLS 1.0/1.1
- 連線字串透過 `appsettings.{env}.json` + User Secrets 或環境變數注入

**DBA 實作規範**：
- 密碼類欄位型別：`VARCHAR(72)`（bcrypt）或 `VARCHAR(128)`（Argon2），**禁止以明文型別儲存密碼**
- 需加密欄位型別：`VARBINARY(MAX)` 或 `VARCHAR(MAX)`，註解標明 `-- ENCRYPTED: AES-256-GCM`

**前端 PG 實作規範**：
- 禁止在前端硬編碼任何金鑰、Token、Secret
- 禁止使用前端加密作為唯一防護手段（前端程式碼可被逆向）

---

## A05: Injection（注入，#5，從第 3 下降）

**威脅定義**：不受信任的資料被送入解譯器（SQL、OS shell、LDAP、NoSQL、表達式引擎）當作指令執行。**涵蓋 38 個 CWE**。

**後端 PG 實作規範**：
- **Dapper 查詢一律使用 `@param` 參數化語法，禁止字串串接組 SQL**（本系統原規範，持續強化）
- 禁止使用 `sp_executesql` 搭配動態 SQL
- 後端禁止動態組合 OS 命令（`Process.Start` 搭配使用者輸入）、LDAP query、XPath
- 反序列化僅接受白名單型別

**前端 PG 實作規範**：
- 動態內容一律透過框架跳脫機制（React JSX / Vue Template）
- **禁止 `innerHTML`、`dangerouslySetInnerHTML`、`v-html`**
- 若確需渲染富文本，使用 DOMPurify 或等效的白名單 Sanitizer，並在 Review Critique 中標註此處使用了 unsafe 渲染

---

## A06: Insecure Design（不安全設計，#6）

**威脅定義**：架構層級的防護缺失，無法靠實作修正。

**SA/SD 階段規範**：
- 威脅建模（Threat Modeling）：識別信任邊界、資料流、濫用情境
- 業務邏輯層級的濫用防護設計：限流、冪等性、審計軌跡
- 對「不可逆操作」（扣款、庫存扣減、刪除）設計獨立的防護機制（二次確認、軟刪除、操作前快照）

---

## A07: Authentication Failures（認證失效，#7）

**後端 PG 實作規範**：
- JWT 驗證使用 `Microsoft.AspNetCore.Authentication.JwtBearer`，配置：
  - `ValidateIssuer = true`
  - `ValidateAudience = true`
  - `ValidateLifetime = true`
  - `ClockSkew = TimeSpan.Zero`（或極小值）
- Access Token 過期時間 ≤ 30 分鐘
- Refresh Token 使用 httpOnly Secure Cookie，搭配 rotation 機制
- **登入失敗回應必須統一回傳 `401 Invalid credentials`**，禁止區分「帳號不存在」vs「密碼錯誤」
- 敏感操作（改密、刪帳號、改 email）必須驗證當前密碼或要求 MFA

**前端 PG 實作規範**：
- **Access Token 禁止存入 `localStorage` / `sessionStorage`**——使用記憶體變數（in-memory）或 httpOnly Cookie
- Token 自動刷新邏輯封裝於 API Client，透過 Interceptor 處理 401 回應
- 登出時清除記憶體 Token 並呼叫後端撤銷端點

---

## A08: Software and Data Integrity Failures（軟體與資料完整性失效，#8）

**威脅定義**：無法防止不可信程式碼或資料被當作可信使用。

**實作規範**：
- CI/CD 產物簽章驗證
- 反序列化（JSON.NET、XmlSerializer）僅接受白名單型別
- 前端依賴的 CDN 資源使用 Subresource Integrity（SRI）
- 更新機制（Auto-update）必須驗證簽章

---

## A09: Security Logging and Alerting Failures（安全日誌與警報失效，#9）

**後端 PG 實作規範**：
- 使用結構化日誌（Serilog 或 `ILogger<T>`），**必須記錄**的安全事件：
  - 認證失敗（含來源 IP、時間戳、嘗試的帳號標識）
  - 授權拒絕（含嘗試存取的資源、使用者 ID、拒絕原因）
  - 輸入驗證失敗（含端點、拒絕原因；**不記錄完整輸入值**）
  - 敏感操作（改密、刪除、權限變更）
- **日誌中禁止記錄**：密碼、Token、信用卡號、完整身分證字號、Session ID
- 關鍵安全事件必須觸發警報（SIEM、PagerDuty、Slack 安全頻道）

**前端 PG 實作規範**：
- 提交前移除所有 `console.log` 中的敏感資訊
- 錯誤追蹤服務（Sentry 等）必須設定 PII scrubbing

---

## A10: Mishandling of Exceptional Conditions（例外處理失效，#10，2025 新增）

**威脅定義**：程式未能預防、偵測、回應異常情境。**關鍵原則：fail-safe，不能 fail-open**。

**後端 PG 實作規範**：
- Controller 層必須有全域 Exception Filter（`IExceptionFilter` 或 Middleware）
- 對外回傳統一錯誤結構：
  ```json
  { "error": { "code": "XXX", "message": "..." } }
  ```
- **禁止洩漏**：Stack Trace、SQL 錯誤訊息、DB 連線字串、伺服器路徑、內部 URL
- 內部例外完整記錄至日誌（含 Stack Trace），對外僅回傳安全訊息
- **Fail-Safe 原則具體化**：
  - 授權檢查失敗 → 拒絕存取（不是允許）
  - 加密失敗 → 拒絕儲存（不是明文）
  - 第三方服務不可用 → 拒絕操作（不是跳過驗證繼續）
  - 限流判斷失敗 → 拒絕請求（不是放行）

**前端 PG 實作規範**：
- 全域錯誤邊界（React Error Boundary / Vue errorHandler）
- 對使用者顯示友善訊息，不洩漏 API 內部錯誤結構

**DBA 實作規範**：
- Transaction 失敗必須 rollback，禁止部分提交
- 連線池耗盡時必須有明確錯誤處理，不得降級為無連線狀態執行
