# ADR-001：Multi-Agent 工作流採漸進式導入策略

- **狀態**：已接受
- **日期**：2026-04-18（2026-04-18 修訂：補入 Human-in-the-Loop 機制）
- **提出者**：Orchestrator（經人類核准）
- **影響 Agent**：Orchestrator / SA/SD / backend-pg / frontend-pg / DBA / QA/QC / e2e-test

---

**TL;DR**：多代理工作流漸進導入，Phase 1 納入人類協作。

## 摘要

Multi-agent 團隊採「漸進式導入」策略：先落地 GitHub Labels（含 `cap:human`）+ Issue Template 的最小可行版本；WORKFLOW.md、Orchestrator 守門模式、自動調度器等進階機制刻意延後，待真實痛點出現且符合啟動觸發條件後才導入。同時，Phase 1 即明確納入「Human-in-the-Loop」機制，確保 AI agent 不會假設人類已完成外部協作事項。

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

此外，VeggieAlly 專案本身大量依賴外部資源（LINE Developer Console、Azure 訂閱、OpenAI / Gemini 金鑰、真實裝置測試），agent team 無法自行完成。必須在 Phase 1 就明確建立「人類協作」的識別與通知機制，避免 agent 自我腦補假設或陷入死鎖。

## 決策

**我們選擇：漸進式導入，分三階段演進；Phase 1 即納入 Human-in-the-Loop。**

只在真實痛點出現且達到啟動觸發條件時，才啟動下一階段；避免過度設計。

### Phase 1（現在落地，MVP）

- GitHub Labels：`cap:*`（7 個能力標籤，含 `cap:human`）、`status:*`（4 個狀態標籤）
- Issue Template：`.github/ISSUE_TEMPLATE/feature.yml`（含外部協作需求勾選區）
- Labels 建立腳本：`scripts/setup-github-labels.sh`
- Human-in-the-Loop 三層判斷漏斗（見下一節）
- Orchestrator 行為：維持「派工模式」，依任務分級（L0 / L1 / L2 / L3）直接指派或交由人類

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

---

## Human-in-the-Loop 機制（Phase 1 納入）

### 任務分級擴充：新增 L0

| 等級 | 描述 | 處理方式 |
|------|------|---------|
| **L0 外部阻塞** | 完全或部分依賴人類或外部資源 | 建 issue、標 `cap:human` + `status:blocked`，觸發三重通知 |
| L1 輕量 | 純配置／文件變更 | Orchestrator 直接處理 |
| L2 標準 | 單一領域 agent 可完成 | 派工給對應 agent |
| L3 複雜 | 多 agent 協作 + 有依賴 | worktree 並行 |

### 三層判斷漏斗（嚴格使用原則）

依序檢查，**任一層命中即標 `cap:human`**：

#### 第一層：AI **能不能**做？（物理／權限限制）

需要人類介入的判斷依據：

- 需要實體設備（刷卡、手機 OTP、U2F 金鑰）
- 需要法律身份（簽合約、法人代表）
- 需要 AI 未持有的外部系統帳號（LINE Console、Azure Portal）
- 需要金錢支出（買網域、API 額度、SaaS 訂閱）
- 需要實體訪談／拍照／面談

#### 第二層：AI **該不該**做？（商業判斷／不可逆決策）

需要人類介入的判斷依據：

- 決策錯誤會產生金錢損失
- 決策錯誤會影響品牌形象
- 決策結果難以回滾（刪資料、發公告、寄信給客戶）
- 需要跨部門／跨角色共識（PM / 設計 / 業主）
- 涉及主觀美感／品味判斷（顏色、文案、UX）
- 涉及法律合規、個資、倫理議題

#### 第三層：AI **敢不敢**做？（高確定性需求）

需要人類介入的判斷依據：

- 需求描述模糊，有多種合理解讀
- 缺乏測試資料／樣本
- 缺乏真實環境驗證方式
- 驗收標準不清

### 嚴格使用原則（防止濫用）

以下情況**不得**標 `cap:human`，agent 應自行處理：

- AI 不確定最佳實踐 → 應先查 ADR、文件、既有程式碼
- 實作細節有多種選項 → AI 應自行決策，除非是架構級選擇
- 測試案例難寫 → AI 應自行設計，不得丟給人類
- Debug 卡住 → AI 應持續嘗試，真正卡死才求助

**底線**：`cap:human` 是必要時才啟動，不是 AI 偷懶的藉口。

### 混合型任務處理

實務上大多數任務是「部分 AI、部分人類」，須拆分為獨立子 issue，使用依賴鎖住順序：

```
Issue #100（父卡）：接入 LINE Webhook
├── #101 [cap:human]      取得 Channel Secret          （第一層）
├── #102 [cap:human]      決定 tenant_id 命名規則       （第二層）
├── #103 [cap:backend-pg] 實作 webhook endpoint         （AI 可做，depends-on: #101）
└── #104 [cap:human + cap:qa-qc] 真實裝置驗證           （需人類協助）
```

依賴語法採 `depends-on: #N`，Phase 2 導入後會自動解鎖；Phase 1 期間由 Orchestrator 人工追蹤。

### 三重通知機制（缺一不可）

當有 `cap:human + status:blocked` 的 issue 存在時，必須同時觸發以下三種通知：

1. **Session 開頭提醒**：Orchestrator 每次對話開始時，先掃描並條列所有 `cap:human + status:blocked` 的 issue，提醒人類處理
2. **PR 描述明列**：本次 PR 涉及的所有 `cap:human` 依賴必須在 PR 描述的「待人類處理事項」章節明列，並標示是否已完成
3. **GitHub Assignee 指派**：`cap:human` issue 必須指派 GitHub Assignee 為具體人類帳號（非 agent），利用 GitHub 原生通知機制

---

## 被拒絕的方案

| 方案 | 拒絕理由 |
|------|---------|
| 一次導入全套 pull-based 工作流（狀態機 + 依賴 + 認領鎖 + WIP + 調度器） | 當前團隊規模（單人手動呼叫、被動 agent）無法觸發大多數機制，流程儀式大於實質收益，維護成本高 |
| 完全不導入任何工作流機制 | 未來若並行 agent 增加，缺乏基本追蹤會立即失控；Issue Template 與 Labels 是低成本高回報的起點 |
| 7 狀態完整狀態機（`New / Refined / Ready / Claimed / Blocked / Review / Done`） | `New` 與 `Refined` 對當前規模無差異價值，改採 5 狀態精簡設計並併入 `Backlog`，等 Phase 2 再啟用 |
| Human-in-the-Loop 延後到 Phase 2 | VeggieAlly 專案 Phase 1 即會遇到 LINE API Secret、Azure 訂閱等外部阻塞，現在不納入等於讓 agent 自我腦補假設，損害可靠性 |
| `cap:human` 寬鬆使用（凡事模糊就找人類） | 會讓 agent 喪失問題解決能力，退化為無腦轉發器；因此採嚴格三層漏斗判斷 |

## 後果

### 正面影響

- 立即可用：Issue Template + Labels 不改變 Orchestrator 行為，零遷移成本
- 低維護負擔：無需撰寫、無需執行複雜工作流文件
- 決策有依據：未來導入 Phase 2 / Phase 3 時，有明確觸發條件可驗證
- 避免紙上談兵：不建立用不到的機制
- Human-in-the-Loop 即時可用：VeggieAlly 後續接 LINE／Azure／OpenAI 不會陷入假設死鎖

### 負面影響 / 需注意的取捨

- 若未來團隊快速擴張，Phase 2 導入會稍有 catch-up 成本
- 依賴 Orchestrator 定期檢視「啟動觸發條件」是否達標——若疏忽，可能延遲升級
- Labels 使用紀律仰賴人類與 Orchestrator 自律，無自動化強制
- 三層漏斗的判斷仍有灰色地帶，Orchestrator 需持續校準

## 未來調整機制

三層漏斗判準**保留動態調整空間**：

- 當三層漏斗出現難以歸類的灰色案例時，由 Orchestrator 彙整案例、提出修訂提案
- 重大調整須新增 ADR（ADR-001a 或 ADR-002）記錄變更理由與影響
- 輕微調整可於本 ADR 直接修訂並更新「日期」欄位

## 給下游 Agent 的強制規則

> ⚠️ MUST-READ：以下規則由此 ADR 約束，下游 Agent 不得違反。

- **規則 1**：建立 issue 時必須使用 `.github/ISSUE_TEMPLATE/feature.yml` 表單，必填欄位（問題陳述、驗收標準、Scope 內）不得留空
- **規則 2**：每張 issue 必須同時帶**一個** `cap:*` 能力標籤與**一個** `status:*` 狀態標籤。Phase 1 由 Orchestrator 依 Feature Form 的必填欄位於 triage 補齊；未補齊一律維持／改標為 `status:blocked`，不得進 `status:ready`
- **規則 3**：Phase 2 機制（狀態機、依賴自動解鎖、認領鎖、WIP limit、守門模式）在未正式啟動前，**不得提前使用**；如需使用請先升級 ADR
- **規則 4**：Orchestrator 在每次需求淨化時，除既有的「階段零 ADR 查詢」外，需同步檢視本 ADR 的「啟動觸發條件」是否達標；若達標必須提請人類決定是否升級 Phase
- **規則 5**：任何偏離本 ADR 的流程變更，必須新建 ADR 並明確標示取代或修訂本 ADR
- **規則 6**（Human-in-the-Loop）：Orchestrator 在「階段一需求淨化」時必須套用三層漏斗判斷外部協作需求；命中任一層即拆出 `cap:human` 子 issue 並用 `depends-on` 鎖住後續 AI 任務；禁止在未澄清前讓 AI agent 對外部條件做假設
- **規則 7**（嚴格使用）：`cap:human` 僅在三層漏斗命中時使用，禁止在「AI 不確定」「AI 不想做決定」等情境濫用
- **規則 8**（三重通知）：當存在 `cap:human + status:blocked` 的 issue 時，Orchestrator 必須同時執行「Session 開頭提醒 + PR 描述明列 + GitHub Assignee 指派」三種通知，缺一不可

## 相關 Issue / PR

- Issue: TBD
- PR: TBD
