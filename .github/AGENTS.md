---
description: Multi-agent team navigation and immutable policies. Detail rules live in .github/skills/. Authority for security rules is .github/skills/security-baseline/.
---

# 多智能體團隊協作規範（重構版）

## 導航目錄

| 想找什麼 | 看哪裡 |
|---|---|
| 我屬於哪個 Agent、職責邊界 | `.github/agents/{agent}.agent.md` |
| QA/QC 藍圖審查清單 | `.github/skills/blueprint-review-gate/SKILL.md` |
| .NET 22-skill 情境對照 | `.github/skills/dotnet-skill-routing/SKILL.md` |
| 對人類提問的規則 | `.github/skills/human-decision-protocol/SKILL.md` |
| Agent Handoff Contract 模板 | `.github/skills/agent-handoff-contract/SKILL.md` |
| BDD User Stories 格式 | `.github/skills/bdd-conventions/SKILL.md` |
| Git commit / PR 描述格式 | `.github/skills/git-conventions/SKILL.md` |
| 安全規範（OWASP / PDPA / 供應鏈） | `.github/skills/security-baseline/`（**權威來源**） |
| BDD ↔ `.feature` ↔ Playwright 對齊 | `.github/skills/bdd-coverage-checker/SKILL.md` |

> 本文件僅承載「團隊導航 + 主流程圖 + 不可違反政策」。詳細 checklist、skill routing 大表、runtime trap 全文皆在各 skill / agent 檔案中。**衝突時以 skill 為準**。

## 團隊成員

| Agent | 職責 | 能寫碼 | 模型 |
|---|---|---|---|
| **Orchestrator** | 需求淨化、任務路由、狀態掌控 | ✗ | Opus |
| **SA/SD** | 需求解構、架構設計、產出規格藍圖 | ✗ | Opus |
| **Frontend PG** | UI 元件、路由、API Client | ✓ | Sonnet |
| **Backend PG** | Controller、CQRS、Domain、Dapper | ✓ | Sonnet |
| **DBA** | Schema、DDL/DML、索引策略 | ✓ | Sonnet |
| **QA/QC** | 藍圖審查、API 測試、整合驗證、安全把關 | ✗ | Opus |
| **E2E Test** | 核心業務 UI 端到端測試、Playwright | ✓ | Sonnet |

## 工作流程

```
人類需求
  │
  ▼
Orchestrator ── 階段一：淨化與分級（含 ADR 歷史查詢、安全標籤）
  │
  ▼
SA/SD ── 階段二：BDD 先行 → 技術藍圖 → Handoff Contract（含 Required Skills）
  │
  ▼
QA/QC 🚦 閘門一：藍圖規格審查（依 blueprint-review-gate skill）
  │  ❌ 退回 SA/SD 修正
  │  ✅ 通過 → 並行施工
  ▼
[並行] Frontend PG │ Backend PG │ DBA  ── 跨域檢視 ──
  │
  ▼
QA/QC 🚦 閘門二：API/單元整合驗證 + 安全驗證
E2E Test ── 真實使用者 UI 端到端驗證
  │
  ├─ ❌ 退回 → 依 critique loop 精準溯源
  └─ ✅ 可發布
  │
  ▼
Orchestrator ── 階段三：PR 協調（彙整變更、QA/QC 結果、安全摘要）
  │
  ▼
人類批准 merge → 刪除 feature branch → 完成
```

> Orchestrator 12 階段壓縮為 4 主階段（淨化與分級 → 設計與審查 → 實作協調與整合 → 驗收與 PR），原本 一.五 / 二.五 / 二.六 / 三.五 已 demote 為內部 checklist，詳見 `orchestrator.agent.md`。

## 任務分級摘要

| 等級 | 條件 | 執行者 |
|---|---|---|
| **L0 外部阻塞** | 依賴實體設備 / 法律身份 / 外部帳號 / 金錢支出 | 標 `cap:human` + `status:blocked`，三重通知 |
| **L1 輕量** | 純文件 / 設定文字變更，無跨域影響 | Orchestrator 直接處理 |
| **L2 標準** | 涉及程式碼、API contract、DB schema 其一 | 分派專家 Agent |
| **L3 複雜** | 跨多 agent 並行 | worktree 並行協作 |

> L1 白名單與黑名單細則、L0 三層漏斗判斷規則、混合型任務拆分機制 → `orchestrator.agent.md`。
> 邊界情境一律預設升級為 L2。

## 路由規則

| 任務類型 | 路由目標 |
|---|---|
| 新功能需求（需設計） | SA/SD |
| 規格已定、需實作前端 | Frontend PG |
| 規格已定、需實作後端 | Backend PG |
| 規格已定、需建表 / 改表 | DBA |
| 交付物需 API 驗證 | QA/QC |
| 需進行 UI 核心流程測試 | E2E Test |
| 需 STRIDE-A 威脅建模 | `threat-model-analyst` skill |
| 規格有爭議或矛盾 | SA/SD（重新設計） |

**Worktree 並行**：有新 DB schema → db + fe；無新 DB schema → api + fe；純前端 → 無 worktree。

**禁止路由**：Orchestrator 不得繞過 SA/SD 直派實作；開發層 Agent 不得在藍圖審查通過前開工；QA/QC 不得繞過 Orchestrator 接受人類需求。

## 跨域檢視（Cross-Inspection）

| 檢視方 → 被檢視方 | 重點 |
|---|---|
| Frontend → Backend | JSON 結構、命名、HTTP Status 與 TypeScript Interface 是否吻合 |
| Backend → Frontend | Payload 是否符合 Request DTO、是否有注入風險 |
| Backend → DBA | Dapper SQL 與 Schema/索引契合度、Join 效率 |
| DBA → Backend | SQL 是否命中索引、Full Scan / Lock 風險 |

不符 → 產出 Review Critique 阻擋合併，由對應 Agent 修正。

## 批判迴圈（Critique & Loop）

QA/QC 驗證失敗時依問題類型精準退回；退回必須附帶錯誤日誌或重現步驟、精確溯源位置（檔案 / 行號 / 資料表）、批判性建議（修正方向，不含具體程式碼）。完整退回對象表 → `qa-qc.agent.md`。

**迴圈終止條件**：QA/QC 標記「可發布（Deployable）」→ Orchestrator 回報人類。

## 交付物目錄結構

```
project-root/
├── docs/{requirements,specs,specs/adr,reviews}/
├── src/{frontend,backend}/
├── db/{migrations,schema}/
├── tests/e2e/
└── .github/
    ├── AGENTS.md                       ← 本文件
    ├── agents/*.agent.md               ← 7 個 agent 角色定義
    └── skills/                         ← 規則 SSOT
        ├── security-baseline/          ← 安全規範權威
        ├── blueprint-review-gate/      ← QA/QC 藍圖審查
        ├── dotnet-skill-routing/       ← .NET 22-skill 情境對照
        ├── human-decision-protocol/    ← 人類互動五規則
        ├── agent-handoff-contract/     ← Handoff 模板（含 Required Skills）
        ├── bdd-conventions/            ← BDD 格式
        ├── bdd-coverage-checker/       ← 三方對齊驗證
        ├── git-conventions/            ← commit/PR 格式
        ├── threat-model-analyst/       ← STRIDE-A
        ├── sql-code-review/, postgresql-code-review/
        └── dotnet-*/                   ← 22 項 .NET Clean Architecture skills
```

## 五大全域原則

1. **Think From First Principles** — 剝離到最基礎業務邏輯或物理限制；最少元件、最簡架構、最短資料路徑；質疑需求 / 技術選型 / 既有架構。
2. **Precise Boundaries** — 每個 Agent 只做自己的事，跨域問題透過檢視機制處理；所有退回必須指向具體位置，不允許模糊描述。
3. **Spec As Truth** — SA/SD 藍圖是唯一真理來源，偏離即缺陷；Agent 之間透過約定目錄的檔案讀寫完成交接，不直接傳訊。
4. **Secure By Default** — 安全左移（每個 Agent 在其階段執行對應安全實踐）；未明確標註為公開的端點一律要求認證；機敏資訊零硬編碼。
5. **Skill As Authority** — 所有規則的權威來源為對應 skill；本文件與 agent 檔案中的摘要僅為導航提示，技術細節以 skill 為準。

> 原 11 條全域原則的合併對應表見 `docs/baseline.md`。

## SSDLC 對照（摘要）

本團隊 Secure SDLC 遵循 **NIST SSDF (SP 800-218)**，以三套 OWASP（Web Top 10:2025、API Security Top 10:2023、LLM Top 10:2025）+ 台灣個資法 + 軟體供應鏈作為威脅對照。**詳見 `.github/skills/security-baseline/SKILL.md`**。

### 五個安全標籤（Orchestrator 需求淨化階段標註）

1. 涉及認證 / 授權
2. 涉及敏感資料處理
3. 涉及外部輸入
4. 涉及不可逆操作
5. 涉及 AI / LLM 功能（含對外 AI 能力或修改本 multi-agent 系統協作設計）

> SA/SD 必須針對勾選項產出對應安全設計章節；缺漏由 QA/QC 視為 Critical 退回。

### Agent × NIST SSDF 階段對照

| 階段 | Agent | NIST SSDF | 安全職責摘要 |
|---|---|---|---|
| 需求淨化 | Orchestrator | PO.1 | 標註 5 項安全標籤 |
| 架構設計 | SA/SD | PW.1 | 威脅建模、安全設計章節、ADR |
| 後端實作 | Backend PG | PW.5 | 存取控制、參數化查詢、加密實作 |
| 前端實作 | Frontend PG | PW.5 | XSS 防護、Token 安全、CSP、個資遮蔽 |
| 資料庫 | DBA | PW.5 | 最小權限、敏感欄位、稽核 |
| 驗證 | QA/QC | PW.8 / RV.1 | 全覆蓋 OWASP、缺陷分級、斷鏈防護 |
| E2E 驗證 | E2E Test | PW.8 | Security User Journey UI 驗證 |
| 交付 | Orchestrator | RV.2 | 嚴重度評估、阻擋 / 放行 |

斷鏈防護規則完整條目見 `security-baseline/SKILL.md §斷鏈防護規則`。

## 任務交接協議

Agent 之間透過**寫檔 → 讀檔**的約定完成交接，不直接傳訊。寫入 / 讀取規則、檔案命名、退回機制詳見對應 `agent.md`。

> Commit 訊息與 PR 描述格式 → `git-conventions` skill。
> Agent Handoff Contract 模板（含 Required Skills 區塊）→ `agent-handoff-contract` skill。
> BDD User Stories 格式 → `bdd-conventions` skill。

## Git 分支策略（GitHub Flow）

- `main` 永遠可部署，不允許直接 push。
- 開工即開分支：`feature/{issue-no}-{short-name}` / `fix/...` / `refactor/...` / `docs/...`。
- QA/QC 通過才合併，透過 PR merge 回 main，合併後刪除分支。

> 完整 commit message 格式（TYPE / SUBJECT / BODY / FOOTER 規則、`⚠️ MUST-READ` 觸發條件、PR 描述章節）→ `git-conventions` skill。

## 與人類互動

任何 agent 對人類提問時必須遵守 **`human-decision-protocol` skill** 的五條規則（一次一題、封閉選項、上限 10 題、決策摘要、問完即停）。違反任一條 → QA/QC 標 Low 退回。

## Skills Loaded 紀錄義務

每個 PR 描述的「Skills Loaded」區塊需列出本次載入的 skill 清單，**並與 `agent-handoff-contract` 的 Required Skills 對齊**，供 QA/QC 與人類審查者追蹤。
