---
name: bdd-coverage-checker
description: Coverage consistency rules for blueprint Scenario IDs, executable .feature files, and Playwright test blocks. Use when SA/SD produces a blueprint with .feature output, when E2E Test agent generates Playwright skeletons from .feature, or when QA/QC verifies BDD Artifact Consistency. Three-way alignment is the success criterion (Phase 5 of agent-system-refactor-plan-final).
when_to_use: SA/SD 產出規格藍圖 + .feature 檔時；E2E Test Agent 產出 Playwright 骨架時；QA/QC 整合驗證階段執行 BDD Artifact Consistency 檢查時；CI pipeline 在 PR 階段執行對齊檢查時
---

# BDD Coverage Checker

定義**藍圖 Scenario ID** ↔ **`.feature` 檔 Scenario ID** ↔ **Playwright test block ID** 三方對齊規則，作為 Phase 5 推廣的核心驗證機制。

**為何存在**：依 Karpathy 原文「Leverage by Success Criteria」精神，把規格從「文件審查」升級為「可執行成功標準」。三方對齊讓 agent 進入自我驗證迴圈，缺口即缺陷。

**強制規則（Phase 5 完成後）**：
- 每個 feature 的藍圖 / `.feature` / `.spec.ts` Scenario ID 必須**完全一致**（一對一對應）。
- CI 偵測到不一致 → PR fail。
- QA/QC 報告新增「BDD Artifact Consistency」區塊回報對齊結果。

---

## 一、三方對齊定義

| 來源 | 位置 | Scenario ID 格式 |
|---|---|---|
| **A. 藍圖 BDD Scenarios** | `docs/specs/{feature}-spec.md` §BDD User Stories | `SC-XX-YY` |
| **B. 可執行 `.feature` 檔** | `docs/specs/{feature}.feature` 或 `tests/features/{feature}.feature` | Scenario 標題尾端 `# SC-XX-YY` 或 Tag `@SC-XX-YY` |
| **C. Playwright test block** | `tests/e2e/{feature}.spec.ts` | `test('[SC-XX-YY] ...')` |

**對齊規則**：A.set == B.set == C.set（集合相等）。

---

## 二、SA/SD 產出規範（Phase 5 起）

SA/SD 產出藍圖時**同步產出可解析 / 可執行的 `.feature`**：

1. 在 `docs/specs/` 同層建立 `{feature}.feature`。
2. 每個 Scenario 標題尾端加 `# SC-XX-YY` 或 Gherkin Tag `@SC-XX-YY`：

```gherkin
Feature: {feature 中文名稱}
  作為 {角色}
  我希望 {動作}
  以便 {業務價值}

  @SC-01-01
  Scenario: SC-01-01 — {Scenario 標題}
    Given {前置條件}
    When  {觸發動作}
    Then  {預期結果}
```

3. 藍圖底部 Handoff Contract 中宣告：

```markdown
### BDD Artifacts
- 規格文件：`docs/specs/menu-publish-spec.md` §BDD User Stories（10 個 Scenarios，SC-01-01 ~ SC-01-10）
- 可執行 `.feature`：`docs/specs/menu-publish.feature`
- 預期 Playwright 路徑：`tests/e2e/menu-publish.spec.ts`
```

---

## 三、E2E Test Agent 產出規範

E2E Test Agent 收到 `.feature` 後：

1. 由 generator 工具（Phase 5 第二步建立）產出 `.spec.ts` skeleton，每個 `.feature` Scenario 對應一個 `test()` 區塊。
2. test 描述格式：`[{SC-XX-YY}] {Scenario 標題}`（與既有 `e2e-test.agent.md` 規則一致）。
3. **不得自行新增**未在 `.feature` 中的 test block。

---

## 四、Coverage Checker 演算法

```
Input:
  - blueprint_path:  docs/specs/{feature}-spec.md
  - feature_path:    docs/specs/{feature}.feature
  - spec_path:       tests/e2e/{feature}.spec.ts

Step 1: 從 blueprint 萃取所有 SC-XX-YY ID（regex: `SC-\d{2}-\d{2}`）
        → set_A
Step 2: 從 .feature 萃取所有 Scenario ID（regex: `@SC-\d{2}-\d{2}` 或標題尾的 `SC-\d{2}-\d{2}`）
        → set_B
Step 3: 從 .spec.ts 萃取所有 test block ID（regex: `test\('\[SC-\d{2}-\d{2}\]`）
        → set_C

Output:
  - 三集合的 set_A, set_B, set_C
  - 差集：A - B, B - A, A - C, C - A, B - C, C - B
  - 結論：三集合相等 → PASS；任一差集非空 → FAIL（列出差集）

Exit code: 0 = PASS, 1 = FAIL
```

實作建議（Phase 5 第三步）：

| 階段 | 實作位置 | 形式 |
|---|---|---|
| Phase 5.1 | `refactor/scripts/check-bdd-coverage.sh` | Bash + grep + diff |
| Phase 5.2 | `tools/bdd-coverage/index.ts` | Node CLI（可整合到 monorepo） |
| Phase 5.3 | `.github/workflows/bdd-coverage.yml` | CI gate（PR 階段強制） |

---

## 五、QA/QC 報告新增區塊

QA/QC 在整合驗證階段執行 coverage checker，並在 `docs/reviews/{feature}-review.md` 加：

```markdown
## BDD Artifact Consistency

### Scenario ID 三方對齊
- 藍圖（A）：10 個（SC-01-01 ~ SC-01-10）
- `.feature`（B）：10 個
- `.spec.ts`（C）：10 個
- 結果：✅ 三方完全一致

### 差集（若有）
| 差集 | 內容 | 退回對象 |
|---|---|---|
| A - B | SC-01-08 在藍圖但 .feature 缺失 | SA/SD 補 .feature Scenario |
| C - B | SC-01-99 在 .spec.ts 但 .feature 缺失 | E2E Test Agent 移除無對應 test |
```

---

## 六、Phase 5 推廣計畫

### Phase 5.0 — Pilot 驗證（已完成於 Phase T0）
- ADR-000-template dry-run 案例已建立 `.feature`（`docs/specs/dry-run-adr-template.feature`）。
- 信心 6/10：純文件流程的 token 量測模式可能與真實 feature 不同，需第 2 個 pilot 交叉驗證。

### Phase 5.1 — 第 2 個 Pilot（真實 feature）
- 由人類選定 1 個低風險真實 feature（建議：行為穩定 / 跨域少 / 無安全標籤的 CRUD）。
- SA/SD 產出藍圖 + `.feature` + Handoff Contract 含 `BDD Artifacts` 宣告。
- E2E Test Agent 用 generator skeleton 產出 `.spec.ts`。
- QA/QC 跑 coverage checker（Phase 5.1 用 bash 版）。
- 量測 vs Phase T0 baseline，看 token / 時間 / 退回是否改善。

### Phase 5.2 — 推廣至其他 feature
- 第 2 個 pilot 結果證明改善 → 推廣為**所有新需求的標準產出**。
- 既有 feature **不回溯導入**，避免大量重工。
- 若退回率反而上升 → rollback Phase 5，回退為 Phase 4 終態。

### Phase 5.3 — CI gate
- 把 coverage checker 寫進 `.github/workflows/bdd-coverage.yml`，PR 階段強制執行。
- 若 CI 不通過 → PR fail，無法 merge。

### 退場條件
- Phase 5.1 第 2 個 pilot 顯示 token / 時間 / 退回任一項反向惡化超過 10% → 立即 rollback 並更新 ADR-003。
- SA/SD 反映「.feature 產出成本超過效益」→ 暫停 Phase 5.2，回頭評估。

---

## 七、信心註記

- 「三方對齊機制能降低 QA/QC 退回率」假設信心 **6/10**（< 7 邊界：尚未經 Phase 5.1 真實 feature 驗證；理論成立但實務成本未知）。
- 「Coverage checker 演算法可直接實作」信心 **8/10**（regex 萃取 + 集合運算屬標準操作）。
- 「Phase 5 排在最後不會與其他 Phase 衝突」信心 **8/10**（Phase 1–4 不依賴 `.feature` 產出，可獨立進行）。
