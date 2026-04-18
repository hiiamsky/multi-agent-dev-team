# ADR-001：Multi-Agent 工作流採漸進式導入策略

- **狀態**：已接受
- **日期**：2026-04-18
- **提出者**：Orchestrator（經人類核准）
- **影響 Agent**：Orchestrator / SA/SD / backend-pg / frontend-pg / DBA / QA/QC / e2e-test

---

## 摘要

Multi-agent 團隊採「漸進式導入」策略：先落地 GitHub Labels + Issue Template 的最小可行版本；WORKFLOW.md、Orchestrator 守門模式、自動調度器等進階機制刻意延後，待真實痛點出現且符合啟動觸發條件後才導入。

## 背景

規劃 multi-agent 團隊工作流時，有兩派建議：

1. **全套 pull-based 工作流**：狀態機（7 狀態）、能力標籤、依賴標籤、認領鎖、WIP limit、自動調度器一次到位
2. **漸進式導入**：僅先建立最基礎的追蹤機制，其餘等實際痛點出現再補

當時團隊規模的實際狀況：

- 由人類手動呼叫 agent，一次僅一個 session
- Agent 本身是被動的，不會主動 pull issue
- 尚未出現「多 agent 搶單」或「下游等待上游」的真實案例
- 目前的 ADR 查詢 + Feature branch + MUST-READ commit 旗標已能滿足協作需求

若直接導入全套，存在三個風險：流程儀式化卻無實質觸發、維護負擔超過收益、設計與實際運作脫節。

## 決策

**我們選擇：漸進式導入，分三階段演進。**

只在真實痛點出現且達到啟動觸發條件時，才啟動下一階段；避免過度設計。

### Phase 1（現在落地，MVP）

- GitHub Labels：`cap:*`（6 個能力標籤）、`status:*`（4 個狀態標籤）
- Issue Template：`.github/ISSUE_TEMPLATE/feature.yml`
- Labels 建立腳本：`scripts/setup-github-labels.sh`
- Orchestrator 行為不變：維持「派工模式」，依任務分級（L1 / L2 / L3）直接指派

### Phase 2（延後，WORKFLOW.md + 雙模式 Orchestrator）

延後建立 `docs/WORKFLOW.md`，定義：

- 任務狀態機：`Backlog → Ready → Claimed → Review → Done`（+ `Blocked`）
- 依賴標籤規則：`depends-on: #N` 語法
- 認領鎖：使用 GitHub Assignee 作為排他鎖
- WIP Limit：每個 agent 同時最多 1 張 Claimed 卡
- Definition of Ready 守門條件

同時升級 Orchestrator 為雙模式：

- L1 / L2 任務：維持「派工模式」
- L3 任務：切換為「守門模式」，只負責需求淨化、拆子卡、依賴解鎖，由 agent 依能力 pull

**啟動觸發條件（任一達成即導入）**：

- 出現 2 次以上「多 agent 誤接同一張卡」
- 出現「下游 agent 不知道上游何時交付」造成等待
- 單次 sprint 同時並行 issue 數 ≥ 3 張

### Phase 3（延後，自動化調度）

- GitHub Actions 定時觸發 agent 檢查 Ready 池
- WIP Enforcement（超量自動擋 PR）
- Metrics dashboard（lead time、WIP 統計）

**啟動觸發條件**：

- Phase 2 已穩定運作 ≥ 2 個 sprint
- 每週流轉 issue 數 ≥ 10 張

## 被拒絕的方案

| 方案 | 拒絕理由 |
|------|---------|
| 一次導入全套 pull-based 工作流（狀態機 + 依賴 + 認領鎖 + WIP + 調度器） | 當前團隊規模（單人手動呼叫、被動 agent）無法觸發大多數機制，流程儀式大於實質收益，維護成本高 |
| 完全不導入任何工作流機制 | 未來若並行 agent 增加，缺乏基本追蹤會立即失控；Issue Template 與 Labels 是低成本高回報的起點 |
| 7 狀態完整狀態機（`New / Refined / Ready / Claimed / Blocked / Review / Done`） | `New` 與 `Refined` 對當前規模無差異價值，改採 5 狀態精簡設計並併入 `Backlog`，等 Phase 2 再啟用 |

## 後果

### 正面影響

- 立即可用：Issue Template + Labels 不改變 Orchestrator 行為，零遷移成本
- 低維護負擔：無需撰寫、無需執行複雜工作流文件
- 決策有依據：未來導入 Phase 2 / Phase 3 時，有明確觸發條件可驗證
- 避免紙上談兵：不建立用不到的機制

### 負面影響 / 需注意的取捨

- 若未來團隊快速擴張，Phase 2 導入會稍有 catch-up 成本
- 依賴 Orchestrator 定期檢視「啟動觸發條件」是否達標——若疏忽，可能延遲升級
- Labels 使用紀律仰賴人類與 Orchestrator 自律，無自動化強制

## 給下游 Agent 的強制規則

> ⚠️ MUST-READ：以下規則由此 ADR 約束，下游 Agent 不得違反。

- **規則 1**：建立 issue 時必須使用 `.github/ISSUE_TEMPLATE/feature.yml` 表單，必填欄位（問題陳述、驗收標準、Scope 內）不得留空
- **規則 2**：每張 issue 必須同時帶**一個** `cap:*` 能力標籤與**一個** `status:*` 狀態標籤
- **規則 3**：Phase 2 機制（狀態機、依賴自動解鎖、認領鎖、WIP limit、守門模式）在未正式啟動前，**不得提前使用**；如需使用請先升級 ADR
- **規則 4**：Orchestrator 在每次需求淨化時，除既有的「階段零 ADR 查詢」外，需同步檢視本 ADR 的「啟動觸發條件」是否達標；若達標必須提請人類決定是否升級 Phase
- **規則 5**：任何偏離本 ADR 的流程變更，必須新建 ADR 並明確標示取代或修訂本 ADR

## 相關 Issue / PR

- Issue: TBD
- PR: TBD
