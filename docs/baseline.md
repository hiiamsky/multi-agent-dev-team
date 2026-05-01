# 重構前基線量測（Baseline）

> 量測日期：2026-05-01
> 量測對象：`.github/AGENTS.md` 與 `.github/agents/*.agent.md`
> 用途：作為 Phase 1–5 完成後對照的基準

---

## 1. 文件規模

| 檔案 | 行數 | 主要問題 |
|---|---:|---|
| `.github/AGENTS.md` | 671 | 混合導航 / 流程細節 / 技能目錄 / 安全摘要 / Git 規範 |
| `.github/agents/orchestrator.agent.md` | 429 | 12 個階段（含 一.五 / 二.五 / 二.六 / 三.五）|
| `.github/agents/sa-sd.agent.md` | 384 | 預載 11+ 個 skill、含長模板 |
| `.github/agents/qa-qc.agent.md` | 326 | 全覆蓋驗證規則與 .NET 大表混雜 |
| `.github/agents/backend-pg.agent.md` | 197 | 對照 23 個 dotnet-* skill |
| `.github/agents/dba.agent.md` | 179 | 安全與 SQL 規則可抽離 |
| `.github/agents/e2e-test.agent.md` | 170 | BDD 對應規則可引用 skill |
| `.github/agents/frontend-pg.agent.md` | 129 | 接近可接受 |
| **合計** | **2685** | — |

## 2. 重複規則（DRY 違反）

| 規則 | 出現位置 | 建議去處 |
|---|---|---|
| Task Classification | `AGENTS.md` §任務分級 + `orchestrator.agent.md` §階段一.五 | 保留於 `orchestrator.agent.md` |
| QA/QC 藍圖審查清單 | `AGENTS.md` §QA/QC 藍圖規格審查規則 + `orchestrator.agent.md` §階段二.五 + `qa-qc.agent.md` §階段零 | 抽至 `blueprint-review-gate` skill |
| FallbackPolicy 授權標注 | `AGENTS.md` §後端 PG 編碼規範 + `backend-pg.agent.md` §🛡️ 安全規範 | 保留於 `backend-pg.agent.md` |
| Human Decision Protocol | `AGENTS.md` §與人類互動規則 + `orchestrator.agent.md` §與人類互動規則 | 抽至 `human-decision-protocol` skill |
| .NET 22-skill routing | `AGENTS.md` §.NET Clean Architecture Skill 目錄 + SA/SD + Backend + QA/QC + DBA | 抽至 `dotnet-skill-routing` skill |

`AGENTS.md` 自承「衝突時以 skill 為準」即為 SSOT 違反的自白（line 13）。

## 3. 現行權威來源表

| 主題 | 目前權威 | 重構後權威 |
|---|---|---|
| 安全規範（OWASP / PDPA / 供應鏈） | `.github/skills/security-baseline/` | 不變（保留厚度） |
| BDD User Stories 格式 | `.github/skills/bdd-conventions/` | 不變 |
| Agent Handoff Contract | `.github/skills/agent-handoff-contract/` | Phase 4 升級加 Required Skills |
| Git commit / PR 格式 | `.github/skills/git-conventions/` | 不變 |
| Threat Model | `.github/skills/threat-model-analyst/` | 不變 |
| SQL / PostgreSQL 審查 | `.github/skills/sql-code-review/` + `.github/skills/postgresql-code-review/` | 不變 |
| QA/QC 藍圖審查 | 三處散落 | 新增 `blueprint-review-gate` |
| .NET skill routing | 五處散落 | 新增 `dotnet-skill-routing` |
| Human Decision Protocol | 兩處散落 | 新增 `human-decision-protocol` |

## 4. 全域原則收斂前後對照

| Before（11 條） | After（5 條） | 合併理由 |
|---|---|---|
| 1 第一性原理 + 2 不過度設計 + 3 批判思維 | **Think From First Principles** | 三者本質皆為「思考的起點與限度」 |
| 5 精準溯源 + 6 職責隔離 | **Precise Boundaries** | 「邊界清楚」與「溯源精確」是同一概念的兩面 |
| 8 安全左移 + 9 預設安全 + 10 機敏資訊零硬編碼 | **Secure By Default** | 三者皆為「不主動承擔風險」的反向陳述 |
| 4 規格即法律 + 7 檔案即通訊 | **Spec As Truth** | 規格與檔案皆為「上下游唯一真理來源」 |
| 11 Skill 即權威 | **Skill As Authority** | 獨立保留——SSOT 模型的核心宣告 |

## 5. 量化目標（Phase 5 完成後對照）

| 指標 | 目前 | Phase 5 完成後目標 |
|---|---:|---:|
| `.github/AGENTS.md` 行數 | 671 | ≤ 250 |
| `orchestrator.agent.md` 行數 | 429 | ≤ 220 |
| `sa-sd.agent.md` 行數 | 384 | ≤ 180 |
| `qa-qc.agent.md` 行數 | 326 | ≤ 180 |
| `backend-pg.agent.md` 行數 | 197 | ≤ 140 |
| `dba.agent.md` 行數 | 179 | ≤ 140 |
| `e2e-test.agent.md` 行數 | 170 | ≤ 140 |
| `frontend-pg.agent.md` 行數 | 129 | ≤ 120 |
| 合計 | 2685 | ≤ 1370（理論減量 49%）|
| 重複規則散落點數 | 5 條 × 平均 3 處 | 每條 1 處 |
