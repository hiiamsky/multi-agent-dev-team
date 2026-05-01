---
name: blueprint-review-gate
description: QA/QC blueprint specification review checklist. Use when QA/QC is reviewing a SA/SD blueprint before parallel implementation begins, or when Orchestrator triggers the blueprint review gate. Defines pass/reject conditions, the 5-domain checklist (BDD / API Contract / Schema / Security / Handoff Contract), and the rejection report format.
when_to_use: QA/QC 執行藍圖規格審查（SA/SD commit 藍圖後、Orchestrator 觸發並行施工前）；Orchestrator 驗收 QA/QC 審查結果；SA/SD 在送審前自我檢查
---

# Blueprint Review Gate（跨 Agent 共用）

本 skill 定義 QA/QC **藍圖規格審查閘門**的審查清單、退回條件、通過條件。

**為何存在**：原本 `AGENTS.md`、`orchestrator.agent.md`、`qa-qc.agent.md` 三處各自維護一份審查清單，修改時易不同步。本 skill 為唯一權威來源（SSOT）。

**強制規則**：
- QA/QC 執行藍圖審查時**必須載入本 skill**，不得依個人記憶或其他文件。
- 藍圖中任一檢查項目不通過 → 直接退回 SA/SD，不進入後續驗證（Critical 缺陷）。
- 開發層 Agent（前端 / 後端 / DBA / E2E）**不得在審查未通過時開工**。

---

## 一、執行時機

| 時機 | 觸發者 | 動作 |
|---|---|---|
| SA/SD commit 規格藍圖到 feature branch | SA/SD | 通知 Orchestrator |
| Orchestrator 收到通知 | Orchestrator | 立即觸發 QA/QC，提供藍圖路徑與安全標籤 |
| QA/QC 接收任務 | QA/QC | 載入本 skill + `bdd-conventions` + `agent-handoff-contract`，開始審查 |
| 審查完成 | QA/QC | 寫入 `docs/reviews/{feature-name}-blueprint-review.md`，回報 Orchestrator |

---

## 二、審查清單（五大檢查項目）

### 1. BDD Scenarios

| 檢查點 | 退回條件 |
|---|---|
| Given / When / Then 完整 | 缺任一元素 |
| 場景編號規範（`SC-XX-YY`） | 編號不符 `bdd-conventions` skill |
| Happy Path + 異常 / 邊界情況 | 僅有 Happy Path |
| Then 描述具體 | Then 內容模糊，無法推導 API |

### 2. API Contract

| 檢查點 | 退回條件 |
|---|---|
| Request 結構明確 | 欄位型別、長度、必填性不明確 |
| Response 結構明確 | 欄位排序、型別、可空性不明確 |
| HTTP Method + 路由明確 | RESTful 用法錯誤 |
| 狀態碼完整覆蓋 | 缺少 4xx 或 5xx 錯誤情況 |
| 錯誤訊息格式統一 | 格式不符既有 API 錯誤契約 |

### 3. Schema

| 檢查點 | 退回條件 |
|---|---|
| 表名、欄位名規範 | 命名不符專案慣例 |
| 欄位型別合理 | 不合理的型別選擇 |
| 欄位長度限制存在 | VARCHAR 缺少長度限制 |
| 索引策略明確 | Query 無對應索引支撐 |

### 4. 安全設計（若 Orchestrator 勾選任一安全標籤）

| 檢查點 | 退回條件 |
|---|---|
| 安全章節完整 | 信任邊界、認證、敏感資料、輸入驗證、操作防護任一缺失 |
| 敏感欄位遮蔽規則明確 | 遮蔽規則模糊 |
| 認證授權矩陣完整 | 端點權限要求不明確 |

> 詳細的安全規則以 `security-baseline` skill 為準；本 skill 只檢查「藍圖中是否有對應章節」。

### 5. Agent Handoff Contract

| 檢查點 | 退回條件 |
|---|---|
| 章節存在 | 缺 `## Agent Handoff Contract` 章節 |
| 前提假設明確 | 缺少或模糊 |
| 架構決策表完整 | 決策選擇、被拒方案、理由任一缺失 |
| ADR 引用正確 | 引用不存在的 ADR 或遺漏 ADR |
| 下游提醒具體 | 提醒內容模糊 |
| **Required Skills 區塊**（Phase 4 起）| 缺少或漏列關鍵 skill |

> Handoff Contract 完整模板與必填欄位以 `agent-handoff-contract` skill 為準。

---

## 三、通過 / 退回決策

### 通過條件（所有以下皆 ✅）

- 五大檢查項目全部通過。
- 若有勾選安全標籤，對應安全設計章節存在。
- Agent Handoff Contract 格式正確，含 Required Skills 區塊。
- BDD 推導的 API Contract 與技術藍圖 API 規格一致。

### 退回流程

1. QA/QC 產出 `docs/reviews/{feature-name}-blueprint-review.md`，詳列：
   - 缺陷項目（對應上方五大檢查項目編號）
   - 溯源位置（檔案行號）
   - 修正建議方向（不含具體程式碼）
   - 嚴重度（依 `security-baseline/severity-matrix.md`）
2. SA/SD 在**同一 feature branch** 修正後重新 commit。
3. Orchestrator 再次觸發 QA/QC 重審，直至通過。
4. 通過後，QA/QC 在 review 檔案標記「✅ 藍圖審查通過，可進行並行施工」。

---

## 四、與其他 skill / agent 的關係

| 來源 | 對本 skill 的角色 |
|---|---|
| `bdd-conventions` skill | 提供 BDD 格式與場景編號的詳細規範 |
| `agent-handoff-contract` skill | 提供 Handoff Contract 的完整模板（含 Phase 4 起的 Required Skills） |
| `security-baseline` skill | 提供安全設計的詳細審查清單與分級依據 |
| `qa-qc.agent.md` | 載入本 skill 執行審查 |
| `orchestrator.agent.md` | 觸發本 skill 的執行 |

> 本 skill **不重複** `bdd-conventions` 與 `agent-handoff-contract` 的細節；只列出「審查時要勾選哪些檢查點」。詳細格式查對應 skill。

---

## 五、信心註記

- 本 skill 從三處（`AGENTS.md` §QA/QC 藍圖規格審查規則、`orchestrator.agent.md` §階段二.五、`qa-qc.agent.md` §階段零）收斂而來，內容無遺漏。**信心 9/10**。
- 「五大檢查項目即足夠」的假設信心 **7/10**（< 7 邊界：未來若發現某類缺陷沒被現有清單攔截，需在本 skill 補一條，不得擅自在其他檔案加規則）。
