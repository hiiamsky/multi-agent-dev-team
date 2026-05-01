---
name: agent-handoff-contract
description: Agent Handoff Contract template, required fields definition, and acceptance criteria. Phase 4 upgrade adds the Required Skills section so SA/SD explicitly lists which skills downstream agents must load (replacing default-load-everything strategy). Use when SA/SD is producing a specification blueprint, when Orchestrator is verifying blueprint completeness, or when QA/QC is checking whether the blueprint contains this mandatory section.
when_to_use: SA/SD 產出規格藍圖時（必須在藍圖底部加入此章節）、Orchestrator 驗收 SA/SD 交付物時、QA/QC 執行 blueprint-review-gate 第一步驗證時、後端 / 前端 / DBA 啟動實作前讀取 Required Skills 時
---

# Agent Handoff Contract（v2，Phase 4 升級）

本 skill 定義 Agent Handoff Contract 的**標準模板與必填規則**。**v2 重點：新增 Required Skills 區塊以實踐最小必要載入策略。**

**為何存在**：SA/SD 規格藍圖是下游三個 Agent（前端 / 後端 / DBA）並行施工的起點。Contract 是「交接清單」——缺少它，下游只能猜測上游前提，是系統級缺陷的主因之一。

**強制規則**：
- SA/SD 每份規格藍圖**必須包含** `## Agent Handoff Contract` 章節。
- QA/QC 在 `blueprint-review-gate` 審查時**第一步**驗證此章節是否存在。
- 缺少此章節 → QA/QC **直接退回 SA/SD**，不進後續驗證（Critical 缺陷）。
- **v2 起：Required Skills 區塊缺失或漏列關鍵 skill** → 退回 SA/SD（Medium 缺陷）。

---

## 一、標準模板（v2）

```markdown
## Agent Handoff Contract

> ⚠️ 此章節為強制欄位。缺少此章節，Orchestrator 將退回本藍圖。

### 前提假設（下游 Agent 不得違反）

- （列出下游實作必須遵守的架構前提，例如：欄位格式、資料結構、TTL 設定、加密策略、冪等性設計）

### 架構決策記錄

| 決策主題 | 選擇方案 | 被拒絕方案 | 拒絕理由 |
|---|---|---|---|
| （範例）存儲層 | Redis | PostgreSQL | 草稿 TTL < 24h，不需持久化 |

### ADR 引用

- （若有新建 ADR，列出連結；若無架構決策，填「無」）
- 範例：`ADR: docs/specs/adr/ADR-007-cache-strategy.md`

### Required Skills（v2 新增 — 最小必要載入）

> SA/SD 必須依 `dotnet-skill-routing` 與 `security-baseline` 對照本任務情境，將 skill 分為三類：
> 1. **Required**：本任務**必載**（下游 agent 啟動即載入）。
> 2. **Conditional**：實作中**可能碰到**才載入（需指定觸發條件）。
> 3. **Not Applicable**：明確排除（避免 QA/QC 誤判漏載）。

#### Required（必載）

| Agent | 必載 Skill | 觸發理由 |
|---|---|---|
| backend-pg | `dotnet-cqrs-command`, `dotnet-api-controller`, `dotnet-fluent-validation` | 新增寫入端 API + 驗證 |
| DBA | `postgresql-code-review` | 使用 JSONB 查詢 |
| QA/QC | `blueprint-review-gate`, `security-baseline/severity-matrix.md` | 藍圖審查與缺陷分級 |

#### Conditional（依情境載入）

| Agent | Skill | 觸發條件 |
|---|---|---|
| backend-pg | `dotnet-outbox-pattern` | 若實作中發現需要保證跨服務一致性 |
| backend-pg | `dotnet-domain-events` | 若實作中產生跨 Aggregate 副作用 |

#### Not Applicable（明確排除）

- `dotnet-jwt-authentication`：本任務沿用既有 JWT 設定，不涉及機制變更。
- `dotnet-quartz-jobs`：本任務無排程需求。
- `dotnet-email-*`：本任務不涉及郵件。

### 給各 Agent 的提醒

#### backend-pg 注意
- （哪些介面已凍結、哪些業務規則屬 Domain 層、安全設計關鍵約束）

#### frontend-pg 注意
- （API 回傳格式特殊設計、哪些欄位預設遮蔽、Token 儲存與刷新策略）

#### DBA 注意
- （Schema 設計關鍵約束、敏感欄位加密 / 雜湊要求、稽核欄位必填）
```

---

## 二、Required Skills 填寫規則（v2 核心）

### 步驟

1. SA/SD 完成 BDD User Stories 與技術藍圖後，依下表決定 skill 分類：

| 情境 | 對照來源 | 結果 |
|---|---|---|
| 涉及 .NET 後端實作 | `dotnet-skill-routing` 情境表 | 對應 skill 進 Required 或 Conditional |
| 涉及安全標籤勾選 | `security-baseline` 對應章節 | 進 Required（必載） |
| 涉及 SQL 查詢 | `sql-code-review` / `postgresql-code-review` | 進 Required（DBA + QA/QC） |
| 涉及 BDD 規格產出 | `bdd-conventions`、`agent-handoff-contract` | 進 Required（SA/SD 自身 + QA/QC） |
| 涉及 .NET 但用不到的模式 | EF Core / JWT / Outbox / Quartz / Audit / Email / Health | 進 Not Applicable（明確排除） |

2. **不確定要不要載入** → 進 Conditional 並寫清楚觸發條件，不要進 Required（避免無謂 token 消耗）。

3. **絕對不可以**用「以防萬一全部進 Required」的偷懶策略——這違背 Phase 4 的核心目的。

### PR 描述對齊

PR 描述必須有 `Skills Loaded` 區塊，列出本次 PR 實際載入的 skill 清單。QA/QC 驗證時：

| 對齊規則 | 不一致時動作 |
|---|---|
| Skills Loaded ⊇ Required | 通過 |
| Skills Loaded ⊋ Required（多載入了未列的）| 詢問是否補進 Conditional / Required；若實作確實需要 → 退 SA/SD 補列（Medium）；若多餘 → 退實作 agent 簡化（Low） |
| Skills Loaded ⊉ Required（少載入必要的）| 退實作 agent，要求補載入（Medium 或 High，視缺陷影響）|
| 載入了 Not Applicable 的 skill | 退實作 agent，要求說明或移除（Low）|

---

## 三、Orchestrator 驗收標準

| 驗收項目 | 通過條件 | 不通過處理 |
|---|---|---|
| 章節存在 | 藍圖底部有 `## Agent Handoff Contract` | 退回 SA/SD |
| 前提假設非空 | 至少有 1 條 | 退回 SA/SD 補充 |
| 架構決策有回應 | 填「無」或具體決策 | 退回 SA/SD 補充 |
| **Required Skills 三類齊備（v2）** | Required / Conditional / Not Applicable 三節皆有內容（NA 可寫「無」）| 退回 SA/SD 補充 |
| 給各 Agent 的提醒已針對性填寫 | 至少有 backend-pg 與 DBA 的提醒 | 退回 SA/SD 補充 |
| 若有新 ADR → 文件存在 | `docs/specs/adr/ADR-NNN-*.md` 存在 | 退回 SA/SD 建立 |

---

## 四、QA/QC 驗證規則（v2 升級）

QA/QC 在 `blueprint-review-gate` 審查的第一步：

1. **章節存在性**：缺則 Critical 退 SA/SD，不進後續。
2. **Required Skills 完整性（v2 新增）**：
   - 三類欄位都有內容（NA 可寫「無」）。
   - Required Skills 與藍圖中提到的技術需求對齊（例：藍圖寫 JSONB 查詢但 Required Skills 沒列 `postgresql-code-review` → Medium 退 SA/SD）。
3. **前提假設遵守驗證**（後續整合驗證階段執行）：
   - 前提假設 ↔ 後端 DTO + 前端 TypeScript Interface。
   - 加密 / 遮蔽策略 ↔ DBA Schema + 前端顯示邏輯。
   - 違反則退對應 Agent，不退 SA/SD。
4. **ADR 引用完整性**：
   - `ADR:` 引用 → 確認 ADR 文件實際存在。
   - 含新架構決策但未附引用 → Low 退 Orchestrator 補充。

---

## 五、與 v1 的差異

| 項目 | v1 | v2 |
|---|---|---|
| 強制章節 | 前提假設 / 架構決策 / ADR / 給各 Agent 提醒 | + **Required Skills 三類** |
| 載入策略 | 預設全量載入 22 個 dotnet-* | 最小必要集（依情境） |
| QA/QC 載入策略 | 全覆蓋載入 | 對照 Skills Loaded 與 Required Skills |
| 缺漏處理 | 章節缺 → Critical | + Required Skills 缺漏 → Medium |

---

## 六、信心註記

- v2 模板從 v1 擴充，新增 Required Skills 區塊。**信心 9/10**。
- 「最小必要集策略」假設信心 **7/10**（< 7 邊界：依賴 SA/SD 對 skill 載入需求的判斷準確性，需 Phase T0 pilot 驗證）。
- 「QA/QC 對照規則」假設信心 **8/10**（規則明確、退回路徑明確）。
