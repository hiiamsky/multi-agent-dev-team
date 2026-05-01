# Pilot 基線量測（Phase T0）

> Pilot feature：ADR-000-template dry-run（依 ADR-003 §決策決定使用此 dry-run 案例）
> 對應 Gherkin：`docs/specs/dry-run-adr-template.feature`
> 量測日期：2026-05-01（基線首次填入）

---

## 1. 量測欄位定義

| 欄位 | 定義 | 取得方式 |
|---|---|---|
| **Token 消耗** | 完整跑完一次 SC-T0-01～T0-03 流程的 LLM token 總計（input + output 累加 across all agents） | Phase 5 前由人類手動讀 Anthropic API usage log；Phase 5 後由 token meter 自動填入 |
| **完整流程耗時** | 從 Orchestrator 接收需求到 PR 建立的 wall-clock 時間 | 人類碼錶或 Issue timestamp 差 |
| **QA/QC 退回次數** | 完整流程中 QA/QC 退回 SA/SD 或下游 Agent 的次數（藍圖審查 + 整合驗證合計） | QA/QC 報告計數 |
| **Skills Loaded 平均數** | 各 agent 在本流程中載入的 skill 平均檔案數 | PR 描述 `Skills Loaded` 區塊統計 |
| **agent 啟動到產出第一個動作的時間** | 載入 agent.md + 啟動包 + skill 後到實際 commit 第一個檔案的時間 | 人類觀察或 IDE log |

## 2. 基線數據（重構前）

> 第一次填入時間：尚未執行（待人類於後續 dry-run 時填入）
>
> 填入步驟：
> 1. 用現行 `.github/AGENTS.md` 與 `.github/agents/*.agent.md`（不採用 refactor/ 版本）
> 2. 跑一次完整 SC-T0-01 流程（產出一個 dry-run ADR-XXX）
> 3. 把以下表格各欄位填入

| 指標 | 第 1 次量測 | 第 2 次量測 | 平均 |
|---|---:|---:|---:|
| Token 消耗（萬） | _待填_ | _待填_ | _待填_ |
| 完整流程耗時（分鐘） | _待填_ | _待填_ | _待填_ |
| QA/QC 退回次數 | _待填_ | _待填_ | _待填_ |
| Skills Loaded 平均數 | _待填_ | _待填_ | _待填_ |
| agent 啟動到第一動作時間（秒） | _待填_ | _待填_ | _待填_ |

## 3. 重構各 Phase 完成後的對照數據

> 每個 Phase 完成後，重跑同一流程並填入下表。任何指標反向惡化即觸發該 Phase rollback（依 ADR-003 §後果）。

| 指標 | Baseline | Phase 1 後 | Phase 2 後 | Phase 3 後 | Phase 4 後 | Phase 5 後 | 短期目標 | 長期目標 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Token 消耗（萬） | _待填_ | — | — | — | — | — | ↓ ≥ 30% | ↓ ≥ 50% |
| 完整流程耗時（分鐘） | _待填_ | — | — | — | — | — | ↓ ≥ 20% | ↓ ≥ 40% |
| QA/QC 退回次數 | _待填_ | — | — | — | — | — | ↓ ≥ 1 次 | ↓ ≥ 2 次 |
| Skills Loaded 平均數 | _待填_ | — | — | — | — | — | 5–16 → 2–5 | 2–5 |

## 4. 退場條件

若以下任一條件命中，本 dry-run 策略需回頭重新評估：

1. **基線量測無法在 1 週內完成**（找不到合適的人類 dry-run 執行者）→ 暫停重構，回 ADR-003 重新討論。
2. **第一次 dry-run 結果顯示 token 消耗已低於 5 萬**（代表現況其實沒那麼糟，「Simplify 違反」的判斷需修正）→ 回 ADR-003 §背景修正診斷依據。
3. **任何 Phase 完成後 token 消耗反向惡化超過 10%** → 該 Phase 立即 rollback，並更新本檔記錄原因。

## 5. 信心評估

| 項目 | 信心 | 備註 |
|---|---:|---|
| ADR-000-template 作為 dry-run 候選的代表性 | 7/10 | < 7 邊界：純文件流程的 token 消耗模式可能與實際 feature 開發（含 src/ db/ 變更）不同；要在 Phase 5 補做第 2 個 pilot（含真實 feature）才能交叉驗證 |
| 量測欄位涵蓋本次重構成功標準 | 8/10 | 五個欄位覆蓋 token / 時間 / 退回 / skill 載入；缺的是「人類滿意度」（無法量化，故略） |
| 重構前後可比性 | 7/10 | < 7 邊界：人類同一個任務跑兩次本身會學習，第二次本來就更快；緩解：採用「不同人類 + 同一 dry-run」的兩次量測平均 |
