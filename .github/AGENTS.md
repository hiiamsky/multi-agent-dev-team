---
description: Multi-agent team coordination rules for enterprise software development. Defines agent roles, routing logic, critique loops, and collaboration protocols. Security rules are summarized here; authoritative details reside in .github/skills/security-baseline/.
---

# 多智能體團隊協作規範

## 權威來源聲明

本文件定義**團隊結構、工作流程、路由規則、Git 策略**——這些是每個 Agent 都需要共同遵循的戰略規範。

**安全相關規則** (OWASP Web Top 10:2025、API Security Top 10:2023、LLM Top 10:2025、個資法合規、供應鏈工具鏈、缺陷分級矩陣) 的**權威來源為 `.github/skills/security-baseline/`**。本文件僅收錄摘要與索引,詳情請載入對應 skill 章節。

> ⚠️ 若本文件與 `security-baseline` skill 內容有衝突,**以 skill 為準**。本文件的安全規則摘要僅為戰略提示,不作為技術細節的最終依據。

## 團隊成員

| Agent | 職責 | 能寫碼 | 模型 |
|-------|------|--------|------|
| **Orchestrator** | 需求淨化、任務路由、狀態掌控 | ✗ | Opus |
| **SA/SD** | 需求解構、架構設計、產出規格藍圖 | ✗ | Opus |
| **前端 PG** | UI 元件、路由、API Client | ✓ | Sonnet |
| **後端 PG** | Controller、CQRS、Domain、Dapper | ✓ | Sonnet |
| **DBA** | Schema、DDL/DML、索引策略 | ✓ | Sonnet |
| **QA/QC** | API 測試、整合驗證、破壞性測試 | ✗ | Opus |
| **E2E 測試** | 核心業務流程端到端測試、Playwright 控制 | ✓ | Sonnet |

## 工作流程

```
人類需求
  │
  ▼
┌─────────────┐
│ Orchestrator │  階段一:需求淨化
│  (PM)        │  ── 不合理 → 退回人類
└──────┬──────┘  ── 合理 → 精煉後交付 SA/SD
       │
       ▼
┌─────────────┐
│   SA/SD      │  階段零:BDD User Stories(Given/When/Then 強制產出)
│              │  階段二:架構設計
│              │  ── 產出標準化藍圖(API Contract 從 BDD Then 推導 + Schema + 時序)
└──────┬──────┘
       │
       ├──────────────────────────┐
       ▼                          ▼
┌──────────────────┐   ┌──────────────────┐
│  worktree: fe    │   │  worktree: db    │  階段三:並行施工(worktree)
│  前端 PG         │   │  DBA             │  ── 各自在獨立 worktree 開工
└────────┬─────────┘   └────────┬─────────┘
         │                      │
         │              DBA commit 後
         │                      │
         │             ┌────────▼─────────┐
         │             │  worktree: api   │
         │             │  後端 PG         │  ── 等 DBA commit 後開工
         │             └────────┬─────────┘
         │                      │
         │    跨域檢視 (Cross-Inspection)
         │◄────────────────────►│
         │                      │
         └──────────┬───────────┘
                    │
                    ▼
            ┌──────────────┐
            │ Orchestrator │  階段三.五:Worktree Integration
            │              │  ── 整合各 worktree、清除分支、衝突退回機制
            └──────┬───────┘
                   │
                   ▼
            ┌──────────────┐
            |   QA/QC      |  階段四:API 層/單元整合驗證(BDD Scenario Coverage 100%)
            |   E2E 測試   |  階段五:真實使用者 UI 端到端驗證與批判迴圈
            └──────┬───────┘
                   |
           ┌───────┴───────┐
           ▼               ▼
     ✅ 可發布        ❌ 退回修正
     → Orchestrator    → 溯源退回對應 Agent
       │                 → 從前端 UI 到後端 API 追查 Trace/Log
       ▼
┌──────────────┐
│ Orchestrator │  階段六:PR 協調
│              │  ── 彙整變更摘要、建立 PR
└──────┬───────┘
       │
       ▼
  人類批准 merge → 刪除 feature branch → 任務完成
```

## 任務分級 (Task Classification)

**核心原則**:無論任何等級,Issue + Feature Branch 一律必建,不得跳過
(Git 是跨 session 的唯一持久記憶體)。

| 等級 | 判斷條件 | 執行者 |
|------|----------|--------|
| **L1 輕量** | 純文件 / 設定變更、無跨域影響、無任何程式碼異動 | Orchestrator 直接處理 |
| **L2 標準** | 涉及程式碼、API contract、DB schema 其中之一 | 分派給對應專家 Agent |
| **L3 複雜** | 跨多個 agent 職責、需要並行開發 | worktree 並行,多 Agent 協作 |

**L1 的範疇**(白名單,完整清單詳見 `orchestrator.agent.md`):

- ✅ 更新 `.md` 文件(README、AGENTS.md、agent 檔案、ADR、SKILL 章節)
- ✅ 修改 `.github/ISSUE_TEMPLATE/feature.yml` 的文字欄位
- ✅ 修正 commit message 規範、分支命名慣例的文字描述

**逾越 L1 範疇的情境必須分級為 L2**:

- ❌ 任何 `src/`、`db/`、`tests/` 下的檔案異動
- ❌ `*.csproj`、`*.json`、`*.yml` 的結構性變更(非純文字)
- ❌ **新增或刪除 agent 檔案** → 屬於修改 multi-agent 協作設計,必須勾選「AI / LLM 功能」安全標籤
- ❌ **Skill 檔案的規則變動** → 會影響所有 agent 行為,必須走 L2 + SA/SD 審查

**分級聲明格式**(Orchestrator 每次路由必須明確輸出):

> 任務分級:**L{N}**
> 理由:{一句話說明判斷依據}
> 執行方式:{Orchestrator 直接處理 / 分派給 {Agent 名} / worktree 並行}

## 路由規則

### Orchestrator 分派邏輯

| 任務類型 | 路由目標 |
|----------|----------|
| 新功能需求(需設計) | SA/SD |
| 規格已定、需實作前端 | 前端 PG |
| 規格已定、需實作後端 | 後端 PG |
| 規格已定、需建表/改表 | DBA |
| 交付物需 API 驗證 | QA/QC |
| 需進行全系統 UI 核心流程測試 | E2E 測試 |
| 規格有爭議或矛盾 | SA/SD(重新設計) |

**Worktree 並行規則**:
- 有新 DB schema → `git worktree` 建立 db + fe 兩條並行軌道,backend 等 DBA commit 後開工
- 無新 DB schema → `git worktree` 建立 api + fe 兩條並行軌道
- 純前端 → 無 worktree,直接開工

### 禁止路由

- Orchestrator **不得**直接指派實作任務給前端/後端/DBA/E2E 測試(必須先經 SA/SD 產出藍圖)
- 開發層 Agent **不得**繞過 QA/QC 直接宣告完成
- QA/QC **不得**繞過 Orchestrator 直接接受人類需求

## 平行施工規則

SA/SD 藍圖交付後,前端 PG、後端 PG、DBA 三者**同時啟動**,互不阻塞:

- **前端 PG**:依 API Contract 建立 Mock 資料,不等後端就緒
- **後端 PG**:依 Contract 實作端點,不等前端或 DB 就緒
- **DBA**:依 Schema 設計產出 DDL/DML,不等後端查詢邏輯

三者初步完成後,進入**跨域檢視**階段。

## 跨域檢視 (Cross-Inspection)

| 檢視方 | 被檢視方 | 檢視重點 |
|--------|----------|----------|
| 前端 PG | 後端 PG | JSON 結構、屬性命名、HTTP Status 是否與 TypeScript Interface 吻合 |
| 後端 PG | 前端 PG | 前端 Payload 是否符合 Request DTO、是否有注入風險 |
| 後端 PG | DBA | Dapper SQL 與 Schema/索引是否契合、Join 效率 |
| DBA | 後端 PG | SQL 語法是否命中索引、是否有 Full Table Scan 或 Table Lock 風險 |
| 後端 PG | 前端 PG | 前端是否正確處理 Token 傳遞、是否有 XSS 風險點 |
| 後端 PG | DBA | DB 帳號權限是否符合最小權限原則 |
| DBA | 後端 PG | 敏感欄位的存取是否使用了對應的加密/雜湊處理 |

**檢視結果**:
- 吻合 → 提交 QA/QC
- 不符 → 產生 Review Critique,阻擋合併,由對應 Agent 修正

## 批判迴圈 (Critique & Loop)

QA/QC 驗證失敗時,依問題類型精準退回:

| 問題類型 | 退回對象 |
|----------|----------|
| API Contract 不符 / Payload 結構錯誤 | 後端 PG |
| Dapper 查詢超時 / SQL 效能問題 | 後端 PG + DBA |
| 畫面渲染錯誤 / 前端型別不符 | 前端 PG |
| Schema 導致 Deadlock / 連線池耗盡 | DBA |
| 規格本身模糊或矛盾 | SA/SD |
| 需求本身有問題 | Orchestrator |
| 安全缺陷(分級與溯源規則詳見 skill) | 依 severity-matrix.md 溯源退回 |

**退回必須附帶**:
1. 錯誤日誌或重現步驟
2. 精確溯源位置(檔案/行號/資料表)
3. 批判性建議(修正方向,不含具體程式碼)

**迴圈終止條件**:QA/QC 標記「可發布(Deployable)」→ Orchestrator 回報人類。

**安全缺陷的分級判斷**詳見 `.github/skills/security-baseline/severity-matrix.md`。

## 交付物目錄結構

所有 Agent 的產出必須放在約定目錄,從約定位置讀取上游交付物。這是 Agent 之間的唯一通訊管道。

```
project-root/
├── docs/
│   ├── requirements/      ← Orchestrator:精煉後的需求文件
│   ├── specs/             ← SA/SD:標準化藍圖(API Contract + Schema + 時序)
│   │   └── adr/           ← SA/SD:架構決策記錄(ADR)
│   └── reviews/           ← QA/QC:驗證報告、安全驗證報告與批判回饋
├── src/
│   ├── frontend/          ← 前端 PG:UI 元件、路由、API Client
│   └── backend/           ← 後端 PG:Controller、CQRS、Domain、Infrastructure
├── db/
│   ├── migrations/        ← DBA:Migration 腳本(依序編號)
│   └── schema/            ← DBA:DDL/DML、索引定義
├── tests/
│   └── e2e/               ← E2E 測試:Playwright 腳本
└── .github/
    ├── AGENTS.md                            ← 本文件(團隊總規範)
    ├── agents/                              ← Agent 定義
    │   ├── orchestrator.agent.md
    │   ├── sa-sd.agent.md
    │   ├── backend-pg.agent.md
    │   ├── frontend-pg.agent.md
    │   ├── dba.agent.md
    │   ├── qa-qc.agent.md
    │   └── e2e-test.agent.md
    ├── skills/
    │   ├── security-baseline/               ← 安全規範權威來源
    │   │   ├── SKILL.md                     ← 總索引、適用對象對照表、斷鏈防護規則
    │   │   ├── owasp-web-top10.md           ← OWASP Web Top 10:2025
    │   │   ├── owasp-api-top10.md           ← OWASP API Security Top 10:2023
    │   │   ├── owasp-llm-top10.md           ← OWASP LLM Top 10:2025
    │   │   ├── supply-chain-tooling.md      ← 軟體供應鏈工具鏈(SCA/SBOM/lockfile)
    │   │   ├── pdpa-compliance.md           ← 台灣個資法合規
    │   │   └── severity-matrix.md           ← 缺陷分級矩陣
    │   ├── threat-model-analyst/            ← STRIDE-A 威脅建模執行工具（/threat-model-analyst）
    │   │   └── SKILL.md                     ← 含 references/ 子目錄（skill 內部資源，無需直接引用）
    │   ├── sql-code-review/                 ← 跨資料庫 SQL 安全審查（DBA 跨域檢視、QA/QC §A05）
    │   │   └── SKILL.md
    │   ├── postgresql-code-review/          ← PostgreSQL 特定最佳實踐（JSONB / ENUM / RLS / GIN）
    │   │   └── SKILL.md
    │   ├── git-conventions/                 ← Git commit 格式、分支命名、PR 描述規範
    │   │   └── SKILL.md
    │   ├── bdd-conventions/                 ← BDD Story/Scenario 格式、SC-XX 編號、API 推導規則
    │   │   └── SKILL.md
    │   ├── agent-handoff-contract/          ← Handoff Contract 模板與必填欄位定義
    │   │   └── SKILL.md
    │   │
    │   │   ────── .NET Clean Architecture Skills(來源:ronnythedev/dotnet-clean-architecture-skills)──────
    │   ├── dotnet-clean-architecture/       ← 解決方案骨架、分層、DI 設定
    │   │   └── SKILL.md
    │   ├── dotnet-domain-entity/            ← Domain Entity、Value Object、Factory
    │   │   └── SKILL.md
    │   ├── dotnet-cqrs-command/             ← MediatR Command + Handler + Validator
    │   │   └── SKILL.md
    │   ├── dotnet-cqrs-query/               ← MediatR Query + Dapper + Response DTO
    │   │   └── SKILL.md
    │   ├── dotnet-api-controller/           ← REST Controller、授權、版本管理
    │   │   └── SKILL.md
    │   ├── dotnet-result-pattern/           ← Result<T> 錯誤處理模式
    │   │   └── SKILL.md
    │   ├── dotnet-fluent-validation/        ← FluentValidation 規則與 Pipeline 整合
    │   │   └── SKILL.md
    │   ├── dotnet-repository-pattern/       ← Repository 介面與 EF Core 實作
    │   │   └── SKILL.md
    │   ├── dotnet-ef-core-configuration/    ← EF Core Fluent API、關聯、索引
    │   │   └── SKILL.md
    │   ├── dotnet-dapper-query/             ← Dapper Multi-mapping、分頁、CTE
    │   │   └── SKILL.md
    │   ├── dotnet-specification-pattern/    ← Specification 可組合查詢邏輯
    │   │   └── SKILL.md
    │   ├── dotnet-domain-events/            ← Domain Events、Handler、Outbox 整合
    │   │   └── SKILL.md
    │   ├── dotnet-pipeline-behaviors/       ← MediatR Logging / Validation / Transaction 行為
    │   │   └── SKILL.md
    │   ├── dotnet-outbox-pattern/           ← Outbox 訊息表、背景處理、冪等性
    │   │   └── SKILL.md
    │   ├── dotnet-quartz-jobs/              ← Quartz.NET 排程任務、Cron 設定
    │   │   └── SKILL.md
    │   ├── dotnet-jwt-authentication/       ← JWT Bearer 認證、Refresh Token
    │   │   └── SKILL.md
    │   ├── dotnet-permission-authorization/ ← 權限型授權、Policy Provider
    │   │   └── SKILL.md
    │   ├── dotnet-audit-trail/              ← IAuditable、EF Interceptor、Soft Delete
    │   │   └── SKILL.md
    │   ├── dotnet-health-checks/            ← 依賴監控(PostgreSQL / HTTP / Custom)
    │   │   └── SKILL.md
    │   ├── dotnet-email-sendgrid/           ← SendGrid 郵件整合、模板
    │   │   └── SKILL.md
    │   ├── dotnet-email-aws-ses/            ← AWS SES 郵件整合、本地模板
    │   │   └── SKILL.md
    │   ├── dotnet-unit-testing/             ← xUnit + NSubstitute + FluentAssertions
    │   │   └── SKILL.md
    │   └── dotnet-integration-testing/      ← WebApplicationFactory + Testcontainers + Respawn
    │       └── SKILL.md
    └── ISSUE_TEMPLATE/
        └── feature.yml                      ← Issue 模板(含 5 個安全標籤)
```

## 任務交接協議

Agent 之間不直接傳訊息,而是透過**寫檔 → 讀檔**的約定完成交接。

> 📖 **相關 Skill**：
> - BDD User Stories 的格式規範 → `bdd-conventions` skill
> - Agent Handoff Contract 的模板與必填欄位 → `agent-handoff-contract` skill
> - Commit 訊息與 PR 描述格式 → `git-conventions` skill

### 寫入規則(上游)

| 階段 | 寫入者 | 寫入位置 | 檔案命名慣例 |
|------|--------|----------|--------------|
| BDD User Stories | SA/SD | `docs/specs/` | `{feature-spec}.md` 頂部 `## BDD User Stories` 章節 |
| 需求淨化 | Orchestrator | `docs/requirements/` | `{feature-name}.md` |
| 架構設計 | SA/SD | `docs/specs/` | `{feature-name}-spec.md` |
| ADR | SA/SD | `docs/specs/adr/` | `ADR-{NNN}-{short-name}.md` |
| 前端實作 | 前端 PG | `src/frontend/` | 依專案框架慣例 |
| 後端實作 | 後端 PG | `src/backend/` | 依 Clean Architecture 分層 |
| 資料庫   | DBA | `db/migrations/`, `db/schema/` | `{nnnn}_{description}.sql` |
| 驗證報告 | QA/QC | `docs/reviews/` | `{feature-name}-review.md` |
| E2E 測試 | E2E 測試 | `tests/e2e/` | `{feature-name}.spec.ts` |

### 讀取規則(下游)

| 讀取者 | 必須先讀取 | 來源位置 |
|--------|-----------|----------|
| SA/SD | Orchestrator 精煉需求 | `docs/requirements/` |
| 前端 PG | SA/SD 藍圖 | `docs/specs/` |
| 後端 PG | SA/SD 藍圖 | `docs/specs/` |
| DBA | SA/SD 藍圖 | `docs/specs/` |
| QA/QC | SA/SD 藍圖 + 各 Agent 產出 | `docs/specs/` + `src/` + `db/` |
| E2E 測試 | SA/SD 藍圖 BDD 章節 + 前端 UI | `docs/specs/` + `src/frontend/` |

### 跨域檢視讀取

| 檢視者 | 額外讀取 | 來源位置 |
|--------|----------|----------|
| 前端 PG | 後端 API 實作 | `src/backend/` |
| 後端 PG | 前端請求封裝 + DBA Schema | `src/frontend/` + `db/schema/` |
| DBA | 後端 Dapper 查詢 | `src/backend/` |

### 退回機制

QA/QC 或跨域檢視發現問題時,將 Review Critique 寫入 `docs/reviews/`,並在文件中標註:
- **退回對象**:哪個 Agent 需修正
- **溯源位置**:精確到檔案路徑與問題描述
- 對應 Agent 修正後,更新原檔並重新提交驗證

## .NET Clean Architecture Skill 目錄

本團隊導入一套 22 項的 `.NET` 實作參考 skill(來源:`ronnythedev/dotnet-clean-architecture-skills`)作為後端 PG、SA/SD、DBA、QA/QC 的**實作層權威參考**。以下為使用規則:

- **觸發原則**:當 Agent 遇到表格中的情境時,**必須**以 `read_file` 載入對應 skill 的 `SKILL.md`,再開始產出程式碼或審查。
- **權威衝突**:若 skill 範例與 `security-baseline` 衝突,**以 `security-baseline` 為準**(安全永遠優先於樣板)。
- **不得因「範例看起來簡單」而跳過 skill 載入**——skill 內含反模式警告與 Best Practices。
- **所有項目一律保留**:EF Core、JWT、Outbox、Quartz、Audit Trail、Email、Health Checks 等企業級模式雖目前專案未使用,但保留作為未來擴充時的統一規範。

### 情境 → Skill 對照表

| 觸發情境 | 對應 Skill | 主要使用者 |
|---------|-----------|-----------|
| 建立新專案骨架 / 分層 DI 設定 | `dotnet-clean-architecture` | SA/SD、後端 PG |
| 設計 Aggregate Root / Value Object / Factory | `dotnet-domain-entity` | 後端 PG |
| 建立寫入端 Command + Handler + Validator | `dotnet-cqrs-command` | 後端 PG |
| 建立讀取端 Query + Handler + DTO | `dotnet-cqrs-query` | 後端 PG |
| 建立 REST Controller / 路由 / 版本管理 | `dotnet-api-controller` | 後端 PG |
| 錯誤處理採用 Result<T> | `dotnet-result-pattern` | 後端 PG |
| 撰寫 Request / Response 驗證規則 | `dotnet-fluent-validation` | 後端 PG |
| 建立 Repository / EF Core 實作 | `dotnet-repository-pattern` | 後端 PG |
| 設計 EF Core Fluent API、關聯與索引 | `dotnet-ef-core-configuration` | 後端 PG、DBA(跨域檢視) |
| 撰寫高效能 Dapper 讀取查詢 | `dotnet-dapper-query` | 後端 PG |
| 設計可組合的查詢邏輯 | `dotnet-specification-pattern` | 後端 PG |
| 實作 Domain Events 與事件傳遞 | `dotnet-domain-events` | 後端 PG、SA/SD |
| 實作 MediatR 橫切關注點(Logging / Transaction / Validation) | `dotnet-pipeline-behaviors` | 後端 PG |
| 實作可靠訊息 Outbox 機制 | `dotnet-outbox-pattern` | 後端 PG |
| 建立排程 / 背景任務 | `dotnet-quartz-jobs` | 後端 PG |
| 實作 JWT Bearer 認證 + Refresh Token | `dotnet-jwt-authentication` | 後端 PG |
| 實作權限型授權 / Policy Provider | `dotnet-permission-authorization` | 後端 PG、SA/SD |
| 建立稽核欄位 / Soft Delete | `dotnet-audit-trail` | 後端 PG、DBA |
| 建立依賴健康檢查 | `dotnet-health-checks` | 後端 PG |
| 郵件整合(SendGrid / AWS SES) | `dotnet-email-sendgrid` / `dotnet-email-aws-ses` | 後端 PG |
| 撰寫單元測試(xUnit + NSubstitute) | `dotnet-unit-testing` | 後端 PG、QA/QC |
| 撰寫整合測試(WebApplicationFactory + Testcontainers) | `dotnet-integration-testing` | 後端 PG、QA/QC |

> 📖 **使用紀律**:每次 PR 描述的「Skills Loaded」區塊需列出本次載入的 skill 清單,供 QA/QC 與人類審查者追蹤。

## 全域原則

1. **第一性原理**:所有 Agent 在做決策前,必須將問題剝離到最基礎的業務邏輯或物理限制
2. **不過度設計**:最少元件、最簡架構、最短資料路徑
3. **批判思維**:質疑需求來源、質疑技術選型、質疑既有架構
4. **規格即法律**:SA/SD 藍圖是唯一真理來源,偏離即缺陷
5. **精準溯源**:所有退回必須指向具體位置,不允許模糊描述
6. **職責隔離**:每個 Agent 只做自己的事,跨域問題透過檢視機制處理
7. **檔案即通訊**:Agent 之間不直接傳訊,所有交接透過約定目錄的檔案讀寫完成
8. **安全左移**:安全不是 QA/QC 的專屬職責——每個 Agent 在其階段執行對應的安全實踐,缺陷越早發現修正成本越低
9. **預設安全**:未明確標註為公開的端點一律要求認證;未明確標註為安全的輸入一律視為不可信
10. **機敏資訊零硬編碼**:所有 Agent 產出的任何檔案中,禁止出現硬編碼的密碼、Token、連線字串、加密金鑰
11. **Skill 即權威**:所有安全規範的權威來源為 `.github/skills/security-baseline/`,本文件與 Agent 檔案中的安全規則摘要皆為戰略提示,技術細節依據以 skill 為準

## SSDLC 對照表(摘要)

本團隊的 Secure SDLC 遵循 **NIST SSDF (SP 800-218)** 框架,以**三套 OWASP 權威規範**作為具體威脅對照:

- **OWASP Top 10:2025** — Web 應用程式
- **OWASP API Security Top 10:2023** — API 層
- **OWASP Top 10 for LLM Applications:2025** — AI / LLM 功能與本 multi-agent 系統自身

另外,**台灣個資法合規**與**軟體供應鏈工具鏈**也列為強制遵守範疇。

> 本表僅列出 Agent 與 NIST SSDF 階段的對應關係。**OWASP 具體條目與威脅對照**詳見 `.github/skills/security-baseline/SKILL.md` §適用對象對照表。

### Agent × NIST SSDF 階段對照

| 階段 | Agent | NIST SSDF | 安全職責 |
|------|-------|-----------|----------|
| 需求淨化 | Orchestrator | PO.1 安全需求 | 標註安全標籤(5 項) |
| 架構設計 | SA/SD | PW.1 安全設計 | 威脅建模、安全設計章節、ADR |
| 後端實作 | 後端 PG | PW.5 安全編碼 | 存取控制、參數化查詢、安全日誌、例外處理、加密實作 |
| 前端實作 | 前端 PG | PW.5 安全編碼 | XSS 防護、Token 安全儲存、CSP 相容、個資遮蔽 |
| 資料庫 | DBA | PW.5 安全編碼 | 最小權限、敏感欄位策略、稽核欄位 |
| 驗證 | QA/QC | PW.8 安全測試, RV.1 漏洞識別 | 全覆蓋 OWASP 檢查、缺陷分級、斷鏈防護 |
| E2E 驗證 | E2E 測試 | PW.8 安全測試 | Security User Journey UI 驗證 |
| 交付 | Orchestrator | RV.2 漏洞回應 | 安全缺陷嚴重度評估、阻擋/放行決策 |

### 五個安全標籤

Orchestrator 在需求淨化階段必須標註以下 5 項安全面向,由 SA/SD 依勾選項目產出對應安全設計章節:

1. **涉及認證 / 授權**
2. **涉及敏感資料處理**
3. **涉及外部輸入**
4. **涉及不可逆操作**
5. **涉及 AI / LLM 功能**(含對外 AI 能力或修改本 multi-agent 系統協作設計)

## 安全交接協議(摘要)

安全需求在 Agent 之間的傳遞採用**逐層攜帶,逐層細化**原則——每個 Agent 依其角色載入 `security-baseline` skill 的對應章節:

| 上游 → 下游 | 安全交接物 | 載入 skill 章節 |
|------------|-----------|----------------|
| Orchestrator → SA/SD | 安全標籤勾選項 | `SKILL.md` §核心原則 + `severity-matrix.md` |
| SA/SD → 後端 PG / 前端 PG / DBA | 安全設計章節(認證策略、加密策略、輸入驗證規則、端點權限矩陣) | 依角色載入 OWASP Web / API / LLM + `pdpa-compliance.md` |
| 後端 PG / 前端 PG / DBA → QA/QC | 程式碼與 Schema 實作 | QA/QC 全覆蓋載入 skill 所有章節 |
| QA/QC → Orchestrator | 安全驗證結果(通過/阻擋 + 缺陷清單) | Orchestrator 依 `severity-matrix.md` 決策 |

### 斷鏈防護

斷鏈防護規則的完整條目(10 條)見 `.github/skills/security-baseline/SKILL.md §斷鏈防護規則`。

核心精神:**每個安全標籤勾選項必須在下游產出對應設計/實作/驗證**,任一環節斷鏈皆視為缺陷退回。

## Git Commit Message 規範

> 📖 **權威來源**：`.github/skills/git-conventions/SKILL.md`。以下為摘要，完整規則（TYPE 清單、FOOTER 必填規則、PR 描述格式、⚠️ MUST-READ 觸發條件）以 skill 為準。

所有 Agent 在產生 commit 時,必須遵守以下格式。

### 格式

```
TYPE: SUBJECT

BODY

FOOTER
```

- 標題列(第一行)必須包含 **TYPE** 與 **SUBJECT**,以冒號加空格分隔
- BODY 與 FOOTER 各以空行隔開,非必填

### TYPE 類型

| TYPE | 說明 | 影響程式碼 |
|------|------|-----------|
| Feat | 新功能 | 有 |
| Modify | 既有功能需求調整的修改 | 有 |
| Fix | 錯誤修正 | 有 |
| Docs | 更新文件(如 README.md) | 沒有 |
| Style | 程式碼格式調整(formatting、缺少分號等) | 沒有 |
| Refactor | 重構,針對已上線功能的程式碼調整與優化,不改變既有邏輯 | 有 |
| Test | 新增測試、重構測試等 | 沒有 |
| Chore | 更新專案建置設定、更新版本號等瑣事 | 沒有 |
| Revert | 撤銷之前的 commit,格式:`Revert: TYPE: SUBJECT (回覆版本:xxxx)` | 有 |

### SUBJECT 主旨

- 不超過 50 個字元
- 英文大寫開頭,中英文都不用句號結尾
- 以祈使句書寫,言簡意賅

### BODY 本文

- 非必填,但若撰寫須說明「改了什麼」與「為什麼而改」
- 每行不超過 72 個字

### FOOTER 頁尾

- 必填,用來標註對應的 GitHub Issue 編號,格式:`issue #N`
- 若包含新架構決策,加入 `ADR: docs/specs/adr/ADR-XXX-...`
- 若影響下游 Agent 決策,加入 `⚠️ MUST-READ`

### 範例

```
Fix: 修正首頁資料載入緩慢問題

- 首頁載入後等待超過 10 秒資料才顯示
    - 將資料改為一次撈取,並暫存在記憶體中

issue #456
```

```
Feat: 新增 Redis 草稿快取機制

- 草稿 TTL < 24h,不需持久化至 PostgreSQL
- 減少 DB 寫入壓力約 60%

issue #123
ADR: docs/specs/adr/ADR-007-cache-strategy.md
⚠️ MUST-READ
```

## Git 分支策略(GitHub Flow)

採用 GitHub Flow——只有一條長期分支 `main`,所有開發在 feature branch 上進行。

### 規則

1. **`main` 永遠可部署**——不允許直接 push 到 main
2. **開工即開分支**——每個任務從 main 切出 feature branch
3. **QA/QC 通過才合併**——feature branch 必須經 QA/QC 標記「可發布」後,透過 PR merge 回 main
4. **合併後刪除分支**——保持 repo 乾淨

### 分支命名慣例

分支名稱必須包含對應的 GitHub Issue 編號:

```
feature/{issue-no}-{short-name}     ← 新功能,如 feature/42-user-login
fix/{issue-no}-{short-name}         ← 錯誤修正,如 fix/57-login-timeout
refactor/{issue-no}-{scope}         ← 重構,如 refactor/63-auth-module
docs/{issue-no}-{topic}             ← 文件更新,如 docs/70-api-spec
```

### 與 Agent 流程的對應

```
1. 人類提出需求
2. Orchestrator 需求淨化 → 通過後建立 GitHub Issue (#N) → 建立 feature branch
   $ git checkout -b feature/{N}-{short-name}

3. SA/SD 產出藍圖 → commit 到 feature branch
4. 前端 PG / 後端 PG / DBA 平行施工 → 各自 commit 到同一 feature branch
5. 跨域檢視 → 若有問題,在 feature branch 上修正並 commit
6. QA/QC 整合驗證
   ├── ✅ 可發布 → 進入步驟 7
   └── ❌ 退回 → 在 feature branch 上修正 → 重新提交 QA/QC
7. Orchestrator 彙整變更摘要 → 建立 PR(含功能摘要 + QA/QC 驗證結果 + `closes #N`)
8. 人類批准 merge → merge 到 main → Issue 自動關閉 → 刪除 feature branch
```

### 禁止事項

- **DO NOT** 直接 commit 到 `main`
- **DO NOT** 在未經 QA/QC 驗證的情況下合併 PR
- **DO NOT** 保留已合併的 feature branch