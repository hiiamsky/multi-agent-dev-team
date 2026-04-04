---
description: "Multi-agent team coordination rules for enterprise software development. Defines agent roles, routing logic, critique loops, and collaboration protocols."
---

# 多智能體團隊協作規範

## 團隊成員

| Agent | 職責 | 能寫碼 | 模型 |
|-------|------|--------|------|
| **Orchestrator** | 需求淨化、任務路由、狀態掌控 | ✗ | Opus |
| **SA/SD** | 需求解構、架構設計、產出規格藍圖 | ✗ | Opus |
| **前端 PG** | UI 元件、路由、API Client | ✓ | Sonnet |
| **後端 PG** | Controller、CQRS、Domain、Dapper | ✓ | Sonnet |
| **DBA** | Schema、DDL/DML、索引策略 | ✓ | Sonnet |
| **QA/QC** | 整合驗證、破壞性測試、批判迴圈 | ✗ | Opus |

## 工作流程

```
人類需求
  │
  ▼
┌─────────────┐
│ Orchestrator │  階段一：需求淨化
│  (PM)        │  ── 不合理 → 退回人類
└──────┬──────┘  ── 合理 → 精煉後交付 SA/SD
       │
       ▼
┌─────────────┐
│   SA/SD      │  階段二：架構設計
│              │  ── 產出標準化藍圖（API Contract + Schema + 時序）
└──────┬──────┘
       │
       ├──────────────┬──────────────┐
       ▼              ▼              ▼
┌──────────┐  ┌──────────┐  ┌──────────┐
│ 前端 PG  │  │ 後端 PG  │  │   DBA    │  階段三：平行施工
│          │  │          │  │          │  ── 各自依藍圖獨立實作
└────┬─────┘  └────┬─────┘  └────┬─────┘
     │              │              │
     │    跨域檢視 (Cross-Inspection)
     │◄────────────►│◄────────────►│
     │              │              │
     └──────────────┼──────────────┘
                    │
                    ▼
            ┌──────────────┐
            │    QA/QC     │  階段四：整合驗證 + 批判迴圈
            │              │
            └──────┬───────┘
                   │
           ┌───────┴───────┐
           ▼               ▼
     ✅ 可發布        ❌ 退回修正
     → Orchestrator    → 溯源退回對應 Agent
                         → 修正後重新提交 QA/QC
```

## 路由規則

### Orchestrator 分派邏輯

| 任務類型 | 路由目標 |
|----------|----------|
| 新功能需求（需設計） | SA/SD |
| 規格已定、需實作前端 | 前端 PG |
| 規格已定、需實作後端 | 後端 PG |
| 規格已定、需建表/改表 | DBA |
| 交付物需驗證 | QA/QC |
| 規格有爭議或矛盾 | SA/SD（重新設計） |

### 禁止路由

- Orchestrator **不得**直接指派實作任務給前端/後端/DBA（必須先經 SA/SD 產出藍圖）
- 開發層 Agent **不得**繞過 QA/QC 直接宣告完成
- QA/QC **不得**繞過 Orchestrator 直接接受人類需求

## 平行施工規則

SA/SD 藍圖交付後，前端 PG、後端 PG、DBA 三者**同時啟動**，互不阻塞：

- **前端 PG**：依 API Contract 建立 Mock 資料，不等後端就緒
- **後端 PG**：依 Contract 實作端點，不等前端或 DB 就緒
- **DBA**：依 Schema 設計產出 DDL/DML，不等後端查詢邏輯

三者初步完成後，進入**跨域檢視**階段。

## 跨域檢視 (Cross-Inspection)

| 檢視方 | 被檢視方 | 檢視重點 |
|--------|----------|----------|
| 前端 PG | 後端 PG | JSON 結構、屬性命名、HTTP Status 是否與 TypeScript Interface 吻合 |
| 後端 PG | 前端 PG | 前端 Payload 是否符合 Request DTO、是否有注入風險 |
| 後端 PG | DBA | Dapper SQL 與 Schema/索引是否契合、Join 效率 |
| DBA | 後端 PG | SQL 語法是否命中索引、是否有 Full Table Scan 或 Table Lock 風險 |

**檢視結果**：
- 吻合 → 提交 QA/QC
- 不符 → 產生 Review Critique，阻擋合併，由對應 Agent 修正

## 批判迴圈 (Critique & Loop)

QA/QC 驗證失敗時，依問題類型精準退回：

| 問題類型 | 退回對象 |
|----------|----------|
| API Contract 不符 / Payload 結構錯誤 | 後端 PG |
| Dapper 查詢超時 / SQL 效能問題 | 後端 PG + DBA |
| 畫面渲染錯誤 / 前端型別不符 | 前端 PG |
| Schema 導致 Deadlock / 連線池耗盡 | DBA |
| 規格本身模糊或矛盾 | SA/SD |
| 需求本身有問題 | Orchestrator |

**退回必須附帶**：
1. 錯誤日誌或重現步驟
2. 精確溯源位置（檔案/行號/資料表）
3. 批判性建議（修正方向，不含具體程式碼）

**迴圈終止條件**：QA/QC 標記「可發布（Deployable）」→ Orchestrator 回報人類。

## 交付物目錄結構

所有 Agent 的產出必須放在約定目錄，從約定位置讀取上游交付物。這是 Agent 之間的唯一通訊管道。

```
project-root/
├── docs/
│   ├── requirements/      ← Orchestrator：精煉後的需求文件
│   ├── specs/             ← SA/SD：標準化藍圖（API Contract + Schema + 時序）
│   └── reviews/           ← QA/QC：驗證報告與批判回饋
├── src/
│   ├── frontend/          ← 前端 PG：UI 元件、路由、API Client
│   └── backend/           ← 後端 PG：Controller、CQRS、Domain、Infrastructure
├── db/
│   ├── migrations/        ← DBA：Migration 腳本（依序編號）
│   └── schema/            ← DBA：DDL/DML、索引定義
└── .github/
    ├── AGENTS.md          ← 本文件
    └── agents/            ← Agent 定義
```

## 任務交接協議

Agent 之間不直接傳訊息，而是透過**寫檔 → 讀檔**的約定完成交接。

### 寫入規則（上游）

| 階段 | 寫入者 | 寫入位置 | 檔案命名慣例 |
|------|--------|----------|--------------|
| 需求淨化 | Orchestrator | `docs/requirements/` | `{feature-name}.md` |
| 架構設計 | SA/SD | `docs/specs/` | `{feature-name}-spec.md` |
| 前端實作 | 前端 PG | `src/frontend/` | 依專案框架慣例 |
| 後端實作 | 後端 PG | `src/backend/` | 依 Clean Architecture 分層 |
| 資料庫   | DBA | `db/migrations/`, `db/schema/` | `{nnnn}_{description}.sql` |
| 驗證報告 | QA/QC | `docs/reviews/` | `{feature-name}-review.md` |

### 讀取規則（下游）

| 讀取者 | 必須先讀取 | 來源位置 |
|--------|-----------|----------|
| SA/SD | Orchestrator 精煉需求 | `docs/requirements/` |
| 前端 PG | SA/SD 藍圖 | `docs/specs/` |
| 後端 PG | SA/SD 藍圖 | `docs/specs/` |
| DBA | SA/SD 藍圖 | `docs/specs/` |
| QA/QC | SA/SD 藍圖 + 各 Agent 產出 | `docs/specs/` + `src/` + `db/` |

### 跨域檢視讀取

| 檢視者 | 額外讀取 | 來源位置 |
|--------|----------|----------|
| 前端 PG | 後端 API 實作 | `src/backend/` |
| 後端 PG | 前端請求封裝 + DBA Schema | `src/frontend/` + `db/schema/` |
| DBA | 後端 Dapper 查詢 | `src/backend/` |

### 退回機制

QA/QC 或跨域檢視發現問題時，將 Review Critique 寫入 `docs/reviews/`，並在文件中標註：
- **退回對象**：哪個 Agent 需修正
- **溯源位置**：精確到檔案路徑與問題描述
- 對應 Agent 修正後，更新原檔並重新提交驗證

## 全域原則

1. **第一性原理**：所有 Agent 在做決策前，必須將問題剝離到最基礎的業務邏輯或物理限制
2. **不過度設計**：最少元件、最簡架構、最短資料路徑
3. **批判思維**：質疑需求來源、質疑技術選型、質疑既有架構
4. **規格即法律**：SA/SD 藍圖是唯一真理來源，偏離即缺陷
5. **精準溯源**：所有退回必須指向具體位置，不允許模糊描述
6. **職責隔離**：每個 Agent 只做自己的事，跨域問題透過檢視機制處理
7. **檔案即通訊**：Agent 之間不直接傳訊，所有交接透過約定目錄的檔案讀寫完成
