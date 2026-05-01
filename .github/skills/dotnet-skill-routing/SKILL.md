---
name: dotnet-skill-routing
description: .NET implementation scenario to skill mapping table. Use when SA/SD is selecting reference skills for backend design, when Backend PG / DBA / QA/QC need to identify which dotnet-* skill to load, or when authoring the Handoff Contract Required Skills section. SSOT for the 22-skill scenario routing originally scattered across AGENTS.md, sa-sd, backend-pg, qa-qc, dba.
when_to_use: SA/SD 在藍圖中標註參考 skill；Backend PG 開始實作前選 skill；QA/QC 驗證 PR Skills Loaded 是否齊全；填寫 Handoff Contract 的 Required Skills 區塊
---

# .NET Skill Routing（跨 Agent 共用）

本 skill 是 `.NET` 22 項實作 skill 的**情境 → skill 對照表 SSOT**。

**為何存在**：原本同一張對照表在 `AGENTS.md`、`sa-sd.agent.md`、`backend-pg.agent.md`、`qa-qc.agent.md`、`dba.agent.md` 各維護一份，最大 token 與維護成本來源。本 skill 為唯一權威。

**強制規則**：
- 任何 agent 需要決定載入哪個 dotnet-* skill 時，**先載入本 skill**，再依對照表載入必要 skill。
- skill 與 `security-baseline` 衝突時 → **以 `security-baseline` 為準**（安全永遠優先於樣板）。
- 不得因「範例看起來簡單」而跳過 skill 載入——skill 內含反模式警告與 Best Practices。
- **所有 22 項一律保留**：EF Core、JWT、Outbox、Quartz、Audit Trail、Email、Health Checks 等企業級模式即使本次未使用，也保留作為未來擴充的統一規範。

---

## 情境 → Skill 對照表（22 項）

| 觸發情境 | 對應 Skill | 主要使用者 |
|---|---|---|
| 建立新專案骨架 / 分層 / DI 設定 | `dotnet-clean-architecture` | SA/SD、Backend PG |
| 設計 Aggregate Root / Value Object / Factory | `dotnet-domain-entity` | Backend PG |
| 建立寫入端 Command + Handler + Validator | `dotnet-cqrs-command` | Backend PG |
| 建立讀取端 Query + Handler + DTO | `dotnet-cqrs-query` | Backend PG |
| 建立 REST Controller / 路由 / 版本管理 | `dotnet-api-controller` | Backend PG |
| 錯誤處理採用 `Result<T>` | `dotnet-result-pattern` | Backend PG |
| 撰寫 Request / Response 驗證規則 | `dotnet-fluent-validation` | Backend PG |
| 建立 Repository / EF Core 實作 | `dotnet-repository-pattern` | Backend PG |
| 設計 EF Core Fluent API、關聯與索引 | `dotnet-ef-core-configuration` | Backend PG、DBA（跨域檢視）|
| 撰寫高效能 Dapper 讀取查詢 | `dotnet-dapper-query` | Backend PG |
| 設計可組合的查詢邏輯 | `dotnet-specification-pattern` | Backend PG |
| 實作 Domain Events 與事件傳遞 | `dotnet-domain-events` | Backend PG、SA/SD |
| 實作 MediatR 橫切關注點（Logging / Transaction / Validation） | `dotnet-pipeline-behaviors` | Backend PG |
| 實作可靠訊息 Outbox 機制 | `dotnet-outbox-pattern` | Backend PG |
| 建立排程 / 背景任務 | `dotnet-quartz-jobs` | Backend PG |
| 實作 JWT Bearer 認證 + Refresh Token | `dotnet-jwt-authentication` | Backend PG |
| 實作權限型授權 / Policy Provider | `dotnet-permission-authorization` | Backend PG、SA/SD |
| 建立稽核欄位 / Soft Delete | `dotnet-audit-trail` | Backend PG、DBA |
| 建立依賴健康檢查 | `dotnet-health-checks` | Backend PG |
| 郵件整合（SendGrid） | `dotnet-email-sendgrid` | Backend PG |
| 郵件整合（AWS SES） | `dotnet-email-aws-ses` | Backend PG |
| 撰寫單元測試（xUnit + NSubstitute） | `dotnet-unit-testing` | Backend PG、QA/QC |
| 撰寫整合測試（WebApplicationFactory + Testcontainers） | `dotnet-integration-testing` | Backend PG、QA/QC |

---

## 載入策略：最小必要集（Minimum Required Skills）

> Phase 4 起，skill 載入由 SA/SD 在 `Agent Handoff Contract` 的 `Required Skills` 區塊明列，下游 agent 嚴格依清單載入。**不再預設全量載入。**

### SA/SD 在藍圖中決定 skill 的步驟

1. 對照本 skill 的情境表，識別本任務涉及的情境。
2. 在 Handoff Contract 中分三類列出：
   - **Required Skills**：本任務**必載**的 skill（已確定會用到）。
   - **Conditional Skills**：實作中**可能碰到**才載入的 skill（含觸發條件）。
   - **Not Applicable**：明確排除的 skill（避免 QA/QC 誤判漏載）。

### 範例（給「商家發布菜單」功能）

```markdown
### Required Skills

| Agent | 必載 Skill | 觸發理由 |
|---|---|---|
| Backend PG | `dotnet-cqrs-command`, `dotnet-cqrs-query`, `dotnet-api-controller`, `dotnet-fluent-validation` | 新增寫入 + 讀取 API 端點 |
| Backend PG | `dotnet-pipeline-behaviors` | Transaction 包覆 Command Handler |
| DBA | `postgresql-code-review` | 使用 JSONB 欄位 |
| QA/QC | `dotnet-cqrs-command`, `dotnet-integration-testing` | 驗證 Command 流程 |

### Conditional Skills

| Agent | Skill | 觸發條件 |
|---|---|---|
| Backend PG | `dotnet-outbox-pattern` | 若實作中發現需要保證跨服務一致性才載入 |
| Backend PG | `dotnet-domain-events` | 若實作中產生跨 Aggregate 副作用才載入 |

### Not Applicable

- `dotnet-jwt-authentication`：本任務沿用既有 JWT 設定，不涉及認證機制變更。
- `dotnet-quartz-jobs`：本任務無排程需求。
- `dotnet-email-*`：本任務不涉及郵件。
```

---

## QA/QC 載入策略

QA/QC 不再預設全覆蓋載入所有 22 項。改為：

1. 讀取 PR 描述 `Skills Loaded` 區塊。
2. 與 Handoff Contract 的 `Required Skills` 對照。
3. 載入相同清單作為實作層反模式檢查基準。
4. 若發現 Handoff Contract 漏列關鍵 skill（例：實作有用 Dapper 卻沒列 `dotnet-dapper-query`）→ 退回 SA/SD 補列，**標記為 Medium 缺陷**。

---

## 信心註記

- 22 項對照表內容從原 `AGENTS.md` §.NET Clean Architecture Skill 目錄逐字搬移，**信心 10/10**。
- 「最小必要集策略可降低 token 而不漏防線」的假設信心 **7/10**（< 7 邊界：依賴 SA/SD 的判斷準確性，需 Phase T0 pilot 驗證）。
- 「QA/QC 不全覆蓋仍能攔住缺陷」的假設信心 **6/10**（< 7 邊界：理論上 SA/SD 漏列即漏防，緩解：QA/QC 必須對照 Required Skills 與實作檔案做一次 sanity check）。
