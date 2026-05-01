# 重構驗證報告

> 驗證日期：2026-05-01
> 驗證對象：`.github/` 與 `docs/` 全部重構產出
> 驗證依據：ADR-003 §決策 KPI

---

## 1. 行數驗證（vs Phase 3 §4 各 agent 上限）

| 檔案 | 重構後行數 | 計畫上限 | 結果 |
|---|---:|---:|---|
| `.github/AGENTS.md` | 201 | ≤ 250（短期）/ ≤ 180（長期） | ✅ PASS（短期目標達標；長期目標尚有 21 行緩衝待後續優化） |
| `orchestrator.agent.md` | 132 | ≤ 220 | ✅ PASS |
| `sa-sd.agent.md` | 105 | ≤ 180 | ✅ PASS |
| `qa-qc.agent.md` | 146 | ≤ 180 | ✅ PASS |
| `backend-pg.agent.md` | 107 | ≤ 140 | ✅ PASS |
| `dba.agent.md` | 110 | ≤ 140 | ✅ PASS |
| `e2e-test.agent.md` | 132 | ≤ 140 | ✅ PASS |
| `frontend-pg.agent.md` | 86 | ≤ 120 | ✅ PASS |

**整體減量**：原 AGENTS.md + 7 agent 檔 = 2478 行 → 重構後 1019 行，**減量 58.8%**（超過 baseline.md 預估的 49% 理論減量）。

> 註：減量幅度比預估更大，原因是 Phase 3 重構時把分散規則徹底集中到對應 skill，agent 檔僅保留「角色邊界 + 啟動順序 + Always/Ask/Never + 輸出格式」骨架。

## 2. 引用一致性

### 2.1 Agent 檔案不再以 AGENTS.md 為詳細規則來源

```
$ grep -rn "依 AGENTS.md §" .github/agents/
✅ 通過 — 無引用
```

### 2.2 五大舊章節全文已從新版 AGENTS.md 移除

| 舊章節關鍵字 | 在新 AGENTS.md 出現次數 | 結果 |
|---|---:|---|
| 「QA/QC 藍圖規格審查規則」 | 0 | ✅ 全文已移除 |
| 「後端 PG 編碼規範」 | 0 | ✅ 全文已移除 |
| 「FallbackPolicy」 | 0 | ✅ 全文已移至 `backend-pg.agent.md` |
| 「情境 → Skill 對照表」 | 0 | ✅ 全文已移至 `dotnet-skill-routing` skill |
| 「規則一：一次一題」 | 0 | ✅ 全文已移至 `human-decision-protocol` skill |

### 2.3 三個新 SSOT skill 在新版 AGENTS.md 導航目錄正確引用

| Skill | 在 AGENTS.md 出現次數 | 結果 |
|---|---:|---|
| `blueprint-review-gate` | 3 | ✅ 導航目錄 + 工作流程 + 任務交接 |
| `dotnet-skill-routing` | 2 | ✅ 導航目錄 + 交付物結構 |
| `human-decision-protocol` | 3 | ✅ 導航目錄 + 與人類互動 + 交付物結構 |

## 3. SSOT 違反消除

| 規則 | 重構前散落點 | 重構後唯一權威 | 結果 |
|---|---:|---|---|
| QA/QC 藍圖審查清單 | 3 處 | `blueprint-review-gate/SKILL.md` | ✅ 1 處 |
| .NET 22-skill routing | 5 處 | `dotnet-skill-routing/SKILL.md` | ✅ 1 處 |
| Human Decision Protocol | 2 處 | `human-decision-protocol/SKILL.md` | ✅ 1 處 |
| FallbackPolicy 授權標注 | 2 處 | `backend-pg.agent.md` | ✅ 1 處 |
| Task Classification | 2 處 | `orchestrator.agent.md`（含內部 checklist + 附錄 A） | ✅ 1 處 |

## 4. Phase 完成度

| Phase | 預期產出 | 實際產出 | 結果 |
|---|---|---|---|
| Phase 0 | ADR-003 + baseline.md | `docs/specs/adr/ADR-003-agent-governance-simplification.md`、`docs/baseline.md` | ✅ |
| Phase T0 | dry-run `.feature` + baseline-pilot.md | `docs/specs/dry-run-adr-template.feature`、`docs/baseline-pilot.md` | ✅ |
| Phase 1 | `AGENTS.md` ≤ 250 行 | 201 行 | ✅ |
| Phase 2 | 3 個 SSOT skill | `blueprint-review-gate` / `dotnet-skill-routing` / `human-decision-protocol` | ✅ |
| Phase 3 | 7 個 agent 檔行數達標 | 全部 PASS（見 §1） | ✅ |
| Phase 4 | `agent-handoff-contract` v2 含 Required Skills | 已升級，加入 Required / Conditional / Not Applicable 三類 | ✅ |
| Phase 5 | Coverage checker 設計 + 推廣計畫 | `bdd-coverage-checker/SKILL.md` 含 5.0–5.3 推廣路徑 | ✅ |

## 5. 紅線守住（依 ADR-003 §決策 + 計畫 §7 不做清單）

| 紅線 | 是否守住 | 證據 |
|---|---|---|
| 不刪除 BDD-first | ✅ | sa-sd.agent.md §階段一仍強制 BDD User Stories 先行 |
| 不移除 QA/QC Blueprint Spec Review Gate | ✅ | qa-qc.agent.md §階段一保留；blueprint-review-gate skill 為新權威 |
| 不削弱 `security-baseline` | ✅ | qa-qc.agent.md 仍要求**全覆蓋載入**；agent 檔載入章節對照保留 |
| 不取消人類批准 merge | ✅ | orchestrator.agent.md §階段四明列「Orchestrator 不自行合併」 |
| 不修改 agent frontmatter `tools` / `model` | ✅ | 7 個 agent 檔的 frontmatter 與原版一致 |
| 不一次性全量引入 Gherkin / CI checker | ✅ | bdd-coverage-checker 採 Phase 5.0 → 5.3 漸進；CI gate 排到最後 |
| 不把所有規則塞回 AGENTS.md | ✅ | AGENTS.md 僅 201 行，章節為導航 + 主流程 + 硬政策 |
| 不把規則從 A agent 搬到 B agent（只能進 skills/）| ✅ | 抽出的 5 條規則全部進 skills/ 或保留在原責任 agent（FallbackPolicy 留 backend-pg、Task Classification 留 orchestrator）|

## 6. 已知限制與後續行動

### 6.1 量測基線尚未填入
- `docs/baseline-pilot.md` 的 token / 時間 / 退回次數欄位仍為 `_待填_`。
- **後續行動**：人類於 1 週內執行一次 ADR-000-template dry-run，填入第 1 次量測；再做第 2 次以取平均。
- 若無法在 1 週內完成 → 觸發 baseline-pilot.md §4 退場條件 1，暫停後續推廣並回 ADR-003 重新討論。

### 6.2 真實 feature pilot 尚未執行
- `bdd-coverage-checker/SKILL.md` Phase 5.1 規劃的「真實 feature pilot」尚未啟動。
- **後續行動**：選定 1 個低風險真實 feature 做交叉驗證；信心 6/10 待提升。

### 6.3 CI gate 尚未實作
- `bdd-coverage-checker/SKILL.md` Phase 5.3 的 `.github/workflows/bdd-coverage.yml` 尚未產出。
- **後續行動**：等 Phase 5.1 第 2 個 pilot 結果證明改善後再實作。

### 6.4 部分長章節（如 SA/SD 安全設計細節）已壓縮
- `sa-sd.agent.md` 從 384 行降至 105 行，部分填寫範例（如 Retention 子表逐欄解釋、安全設計章節的詳細條目）被壓縮為骨架引用。
- **後續行動**：若 SA/SD 在實際使用時發現缺失，由 Orchestrator 評估是否補回 skill 或 agent；不得隨意補回 AGENTS.md。

## 7. 整體信心評估

| 判斷 | 信心 | 備註 |
|---|---:|---|
| 「行數達標」 | 10/10 | 客觀可驗證，全部 PASS |
| 「引用一致性」 | 9/10 | grep 驗證通過；唯一不確定是否有更隱晦的舊引用未被 grep 抓到 |
| 「SSOT 違反消除」 | 9/10 | 5 條重複規則全部收斂到 1 處 |
| 「紅線守住」 | 9/10 | 8 條紅線逐項驗證 |
| 「Phase 4 Required Skills 策略可行」 | 7/10 | < 7 邊界：依賴 SA/SD 判斷準確性；待 Phase T0 量測結果驗證 |
| 「Phase 5 三方對齊機制能降低退回率」 | 6/10 | < 7 邊界：尚未經真實 feature 驗證；理論成立但實務成本未知 |
| 整體重構品質 | 8/10 | 結構清晰、引用乾淨；唯一限制是量測基線待填，但這屬於「ADR-003 §決策決定的人類執行步驟」非本次 Claude 範圍 |

---

## Sources

- [ADR-003](docs/specs/adr/ADR-003-agent-governance-simplification.md)
- [baseline.md](docs/baseline.md)
- [baseline-pilot.md](docs/baseline-pilot.md)
