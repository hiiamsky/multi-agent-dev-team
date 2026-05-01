# Phase T0 — Dry-Run Pilot Feature
#
# 用途：本 .feature 是 ADR-001 §Phase T0 規定的「會失敗的成功標準」。
# 它假設未來會有一個 Agent 流程「依 ADR-000-template 產出一份新 ADR」，
# 透過 Gherkin 的 Given/When/Then 把這個流程的成功標準鎖在這裡。
#
# 為什麼選這個案例：
# - 行為穩定：ADR-000-template 結構已固定，不會因業務需求變動。
# - 跨域少：純文件流程，不涉及 src/ db/ tests/。
# - 無安全標籤：純治理變更，不觸發 OWASP 全覆蓋。
# - 對應到本次重構自身：產出 ADR-001 即為這個流程的真實首次執行。
#
# 預期狀態：本 .feature 在重構前**會失敗**，因為現行流程沒有自動化驗證。
# Phase 5 coverage checker 完成後，將由 Playwright generator 自動產出 .spec.ts 並通過此測試。

Feature: 依 ADR-000-template 產出新 ADR
  作為 Orchestrator 或 SA/SD
  我希望 在面對「需建立新架構決策」的情境時
  能夠 依 ADR-000-template 產出一份結構完整、欄位齊全的 ADR
  以便 下游 Agent 在啟動包中能正確讀取並遵守規則

  Background:
    Given 專案根目錄存在 `docs/specs/adr/ADR-000-template.md`
    And ADR-000-template 包含必填欄位：狀態、日期、提出者、影響 Agent、TL;DR、摘要、背景、決策、被拒絕的方案、後果、給下游 Agent 的強制規則、相關 Issue/PR
    And Agent Handoff Contract skill 已載入

  # === SC-T0-01：Happy Path（重構期間預期會失敗）===
  Scenario: SC-T0-01 — Orchestrator 為新架構決策產出符合 template 的 ADR
    Given 一個需求觸發了新架構決策（例：選擇 Redis 作為草稿快取）
    And 該決策不衝突任何已凍結的 ADR
    When Orchestrator（或 SA/SD）依 ADR-000-template 產出新 ADR
    Then 新 ADR 檔案存在於 `docs/specs/adr/ADR-{NNN}-{short-name}.md`
    And 新 ADR 的 12 個必填欄位全部非空（包含 TL;DR ≤ 15 字）
    And 「被拒絕的方案」表至少有 1 列
    And 「給下游 Agent 的強制規則」至少有 1 條 MUST-READ
    And 對應的 commit 訊息包含 `ADR: docs/specs/adr/ADR-{NNN}-...` footer
    And 若決策影響下游 Agent 行為，commit 訊息包含 `⚠️ MUST-READ` 旗標

  # === SC-T0-02：缺漏欄位 → QA/QC 退回 ===
  Scenario: SC-T0-02 — TL;DR 超過 15 字時 QA/QC 應退回
    Given 一份草擬中的新 ADR 已 commit 到 feature branch
    And 該 ADR 的 TL;DR 為「本決策定義了我們選擇 Redis 作為短期草稿快取的原因與限制」（28 字）
    When QA/QC 執行 blueprint-review-gate skill 的「ADR 引用驗證」
    Then QA/QC 標記此 ADR 為 Low 缺陷（依 severity-matrix.md）
    And 退回對象為「ADR 提出者」（Orchestrator 或 SA/SD）
    And QA/QC 退回報告引用本 .feature SC-T0-02

  # === SC-T0-03：與既有 ADR 衝突 → Orchestrator 階段零退回人類 ===
  Scenario: SC-T0-03 — 新 ADR 推翻已凍結 ADR 時必須先取得人類確認
    Given 已存在 `docs/specs/adr/ADR-007-cache-strategy.md` 狀態為「已接受」
    And 新需求要求改用 Memcached 取代 Redis
    When Orchestrator 執行階段零 ADR 歷史查詢
    Then Orchestrator 識別本次需求與 ADR-007 衝突
    And Orchestrator 退回人類，要求說明為何推翻既有決策
    And 在人類確認前，禁止 SA/SD 開始設計

  # === SC-T0-04：Token 基線量測 ===
  Scenario: SC-T0-04 — 完整流程 token 消耗應記錄為基線
    Given 重構前的現行 governance（AGENTS.md 671 行）
    When 完整跑一次 SC-T0-01 的流程（從需求 → ADR commit）
    Then 流程消耗的 token 數記錄於 `refactor/docs/baseline-pilot.md` 表格
    And 流程耗時記錄於同一表格
    And QA/QC 退回次數記錄於同一表格
    # 此 Scenario 的 Then 在 Phase 5 coverage checker 接入 token meter 後才能自動驗證
    # 在那之前由人類在執行 dry-run 時手動填入

  # === 期望的覆蓋對應（給 Phase 5 coverage checker）===
  # SC-T0-01 → tests/e2e/adr-template-dry-run.spec.ts test('[SC-T0-01] ...')
  # SC-T0-02 → tests/e2e/adr-template-dry-run.spec.ts test('[SC-T0-02] ...')
  # SC-T0-03 → tests/e2e/adr-template-dry-run.spec.ts test('[SC-T0-03] ...')
  # SC-T0-04 → 量測腳本 refactor/scripts/measure-baseline.sh（Phase 5 規劃）
