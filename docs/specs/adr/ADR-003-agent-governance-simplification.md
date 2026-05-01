# ADR-003：Multi-Agent Governance 簡化重構

- **狀態**：已接受/已實施
- **日期**：2026-05-01
- **提出者**：人類 + Claude
- **影響 Agent**：Orchestrator / SA-SD / Backend-PG / Frontend-PG / DBA / QA-QC / E2E-Test（全體）

---

**TL;DR**：以 Test 為單一核心、Ask/Simplify/Precise 為支撐，把 governance 文件從 670+428+383+326 行壓縮到上限為 250+220+180+180，並抽出 3 個 SSOT skill 消除 DRY 違反。

## 摘要

依 Karpathy「Ask/Simplify/Precise/Test」四原則執行 Phase 0–5 重構，目標是讓每個 agent 在更少 context、更少歧義、更清楚成功標準下工作。本 ADR 為重構決策的權威紀錄，後續每個 Phase 完成後在此追加進度註記。

## 背景

現行 governance 文件存在三個結構性問題：

1. **Simplify 違反**：規範本身就是「50 行膨脹到 200 行」的活教材。`AGENTS.md` 670 行、orchestrator.agent.md 428 行、SA/SD 預載 11+ 個 skill、QA/QC 要全覆蓋 OWASP，token 黑洞嚴重。
2. **DRY 違反**：Task Classification、QA/QC 藍圖審查、Human Decision Protocol、.NET skill routing 表等散落 3+ 處，`AGENTS.md` 自承「衝突時以 skill 為準」即為 SSOT 違反的自白。
3. **「規範越多 → 出錯越少」假設未經驗證**：反向假設「規範越多 → 認知負擔越大 → 反而易遺漏」同樣合理，目前無 A/B 數據支撐現有路徑。

## 決策

**我們選擇：Phase 0–5 漸進式重構，以 Test (Leverage by Success Criteria) 為單一核心。**

具體執行內容：
- **Phase 0**：本 ADR + 基線量測（`docs/baseline.md`）。
- **Phase T0**（並行 Phase 0–1）：用 ADR-000-template 作為 dry-run 案例，撰寫會失敗的 `.feature`，量測完整流程的 token / 時間 / 退回次數基線。
- **Phase 1**：`AGENTS.md` 安全瘦身（≤250 行），不改語意。
- **Phase 2**：建立 3 個 SSOT skill（`blueprint-review-gate`、`dotnet-skill-routing`、`human-decision-protocol`）。
- **Phase 3**：7 個 agent 檔案重寫，統一結構，行數達標。
- **Phase 4**：升級 `agent-handoff-contract` skill 加入 `Required Skills` 區塊。
- **Phase 5**：Coverage checker（藍圖 ↔ `.feature` ↔ Playwright 三方對齊）+ 推廣計畫。

## 被拒絕的方案

| 方案 | 拒絕理由 |
|------|---------|
| 維持現狀，僅文字編修 | 不解決 Simplify 與 DRY 結構性問題；token 黑洞不會自動消失 |
| 採用激進路徑（廢除 Orchestrator/QA/QC、移除 Handoff Contract、移除 human-decision-protocol、AGENTS.md ≤50 行） | 經逐條對照 Karpathy 原文後**整體符合度 4.5/10**：原文僅支持 2 條（Anti-Swarm hype、Declarative Leverage），其餘 5 條為**超譯**或**邏輯反向**（例：原文「LLM 不會主動釐清」被誤推為「移除 human-decision-protocol」）。且與企業多 agent + 法遵情境的合規承載不符。 |
| 一次到位全 Phase 並行 | 違反 §7 不做清單「不沒有 ADR 就啟動 Phase 1」「不跳過 Phase T0」的時序保護 |

## 後果

### 正面影響
- 單 PR 平均 token 消耗預期降低（具體目標：較 Phase T0 基線 ↓ ≥ 30%）。
- 修改一條共用規則時只需動一個 skill。
- 新 agent 啟動 1 分鐘內可辨識責任、必讀輸入、必產出輸出。
- 重構本身有 pilot success criteria 作為驗收依據，不流於主觀感覺。

### 負面影響 / 需注意的取捨
- 短期內人類首次閱讀需跳轉多檔（緩解：`AGENTS.md` 頂端放導航目錄）。
- SA/SD 在 Handoff Contract 中漏列 `Required Skills` → 下游缺 skill（緩解：QA/QC 在 blueprint gate 加一條檢查）。
- Phase T0 找不到合適 pilot feature（緩解：本 ADR 已決定使用 ADR-000-template 作 dry-run）。

## 給下游 Agent 的強制規則

> ⚠️ MUST-READ：以下規則由此 ADR 約束，下游 Agent 不得違反。

- **規則 1**：本重構期間，所有重構產出已整合至 `.github/` 與 `docs/`，取代原 `refactor/` 暫存路徑。
- **規則 2**：跳過 Phase T0 直接進 Phase 2 以後屬於違反 §7 不做清單，不允許。
- **規則 3**：`security-baseline` 不拆、不降級；只改引用方式。
- **規則 4**：本 ADR 完成後若需擴充原則或新增 SSOT skill，必須再開新 ADR；不得在本 ADR 補充新規則。
- **規則 5**：四原則中的「Precise」屬本團隊延伸，不歸屬 Karpathy；正式對外引用時需註明。

## 相關 Issue / PR

- Issue: #64
- PR: #65

## Phase 進度紀錄（執行中追加）

| Phase | 狀態 | 完成日 | 備註 |
|---|---|---|---|
| Phase 0 | 完成 | 2026-05-01 | 本 ADR + `baseline.md` |
| Phase T0 | 完成 | 2026-05-01 | dry-run `.feature` + `baseline-pilot.md` |
| Phase 1 | 完成 | 2026-05-01 | `AGENTS.md` 瘦身產出於 `.github/AGENTS.md` |
| Phase 2 | 完成 | 2026-05-01 | 3 個 SSOT skill 建立於 `.github/skills/` |
| Phase 3 | 完成 | 2026-05-01 | 7 個 agent 檔案重寫於 `.github/agents/` |
| Phase 4 | 完成 | 2026-05-01 | `agent-handoff-contract` skill v2 於 `.github/skills/` |
| Phase 5 | 完成 | 2026-05-01 | `bdd-coverage-checker` skill 與推廣計畫 |
