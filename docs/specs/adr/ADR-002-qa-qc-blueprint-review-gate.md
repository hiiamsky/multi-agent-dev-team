# ADR-002: QA/QC 藍圖規格審查閘門

**狀態**: 已提案  
**決策日期**: 2026-04-26  
**決策者**: Orchestrator  
**影響**: SA/SD、QA/QC、Orchestrator、所有開發層 Agent  

## 問題陳述

### 現狀問題
在 ADR-001 多智能體工作流程中，SA/SD 完成規格藍圖後直接進入並行施工階段（前端 PG / 後端 PG / DBA 同時開工）。然而，沒有設置規格品質把關點，導致：
- SA/SD 產出的藍圖可能存在 BDD 場景不完整、API Contract 有歧義、Schema 設計缺陷
- 下游 Agent 開工後才發現設計問題，此時修正成本高、影響範圍大
- 開發層 Agent 浪費時間在「偽工作」（等待藍圖澄清）

### 根本原因
- 缺乏「規格審查」這個明確的閘門
- 設計層與實作層之間沒有品質檢查點
- QA/QC 的職責被侷限於「實作驗證」，未被賦予「規格審查」的責任

## 解決方案

### 1. 引入「QA/QC 藍圖規格審查閘門」（🚦 Gate 1）

在工作流程中插入新的閘門：

```
SA/SD 產出藍圖 commit
    ↓
🚦 閘門 1：QA/QC 藍圖規格審查
    ├─ ❌ 審查失敗 → 生成 Review Critique，退回 SA/SD
    │                禁止開發層 Agent 開工
    └─ ✅ 審查通過 → 標記「可施工」，通知 Orchestrator
    ↓
Orchestrator 觸發並行施工開始
    ├─ 前端 PG worktree
    ├─ DBA worktree
    └─ （等 DBA commit 後）後端 PG worktree
```

### 2. QA/QC 藍圖審查職責

QA/QC 在此閘門的職責是**檢查規格完整性與無歧義性**，而非實作驗證。

#### 審查清單

| 檢查項目 | 檢查點 | 退回條件 |
|---------|--------|---------|
| **BDD Scenarios** | Given/When/Then 格式完整 | Missing Given、When、Then 之任一 |
| | 場景編號規範（SC-XX-YY） | 編號不符合 bdd-conventions |
| | Happy Path + 異常/邊界 | 僅有 Happy Path，無異常情況 |
| | Then 描述足夠具體 | Then 內容模糊、無法推導 API Response |
| **API Contract** | Request 結構明確 | 欄位型別/長度/必填性不明確 |
| | Response 結構明確 | 欄位排序/型別/可空性不明確 |
| | HTTP Method + 路由明確 | RESTful 用法錯誤 |
| | 狀態碼完整 | 缺少 4xx 或 5xx 錯誤情況 |
| | 錯誤訊息格式統一 | 格式不符既有 API Contract |
| **Schema** | 表名、欄位名規範 | 命名不符專案慣例 |
| | 欄位型別合理 | 不合理的型別選擇（如 BIGINT 儲存電話號碼） |
| | 欄位長度限制 | 缺少 VARCHAR 長度限制 |
| | 索引策略存在 | Query 無對應索引支撑 |
| **安全設計** | 若勾選安全標籤，安全章節完整 | 信任邊界、認證、敏感資料、輸入驗證、操作防護 任一缺失 |
| | 敏感欄位遮蔽規則明確 | 遮蔽規則模糊 |
| | 認證授權矩陣完整 | 端點權限要求不明確 |
| **Handoff Contract** | 前提假設明確 | 缺少或模糊 |
| | 架構決策表完整 | 決策選擇、被拒方案、理由缺一 |
| | ADR 引用正確 | 引用不存在的 ADR 或遺漏 ADR |
| | 下游提醒具體 | 提醒內容模糊 |

#### 退回機制

- **撰寫位置**：在 `docs/reviews/` 創建 `{feature-name}-blueprint-review.md` 檔案
- **內容**：詳細列出缺陷、溯源位置（檔案行號）、修正建議
- **限制**：QA/QC 不撰寫具體程式碼，只提出「修正方向」
- **同一 feature branch**：SA/SD 修正後，在同一分支重新 commit，不建新分支

#### 審查通過條件
- 所有檢查清單項目 ✅ 通過
- 無遺漏的安全設計章節（若勾選安全標籤）
- Agent Handoff Contract 格式正確
- BDD 推導的 API Contract 與技術藍圖中的 API 規格一致

### 3. Orchestrator 新增職責

#### 觸發審查
- SA/SD commit 藍圖後，Orchestrator 自動（或由人類觸發）調用 QA/QC
- Orchestrator 不得跳過此閘門直接啟動並行施工

#### 等待審查
- 若 QA/QC 審查未完成，Orchestrator 保持「等待 QA/QC 審查」狀態
- Issue label 設為 `status:review`

#### 處理退回
- 若 QA/QC 退回，Orchestrator 將批判信息通知 SA/SD
- SA/SD 修正後，通知 Orchestrator 重新提交審查
- 循環直至通過

#### 啟動並行施工
- 獲得 QA/QC 審查通過後，Orchestrator 建立 worktree、分派開發層 Agent

### 4. ADR-001 與 ADR-002 的關係

ADR-001 定義了多智能體工作流程的整體框架（階段零至階段六）。  
ADR-002 在 ADR-001 框架內，細化了**設計審查**這個關鍵環節：
- 新增「🚦 閘門 1」在「並行施工」之前
- 明確 QA/QC 在設計層的職責
- 防止不完整設計導致下游返工

## 決策理由

### 為什麼選擇在並行施工前加入審查？

**時機選擇的成本效益分析**：
| 審查時機 | 發現缺陷成本 | 修正成本 | 影響範圍 | 選擇 |
|---------|-----------|---------|---------|------|
| **SA/SD 產出後**（本方案） | 低（規格文件） | 低（改規格） | 僅 SA/SD 返工 | ✅ |
| **並行施工進行中** | 中（代碼+規格） | 中（多 Agent 修正） | 前端+後端+DBA 返工 | ❌ |
| **集成測試時** | 高（完整實現） | 高（大規模重寫） | 全系統返工 | ❌ |

**越早發現缺陷，修正成本越低**。規格缺陷應在「實作前」被發現。

### 為什麼 QA/QC 應負責規格審查？

1. **角色匹配**：QA/QC 的核心職責是「品質把關」，不分設計層還是實作層
2. **視角優勢**：QA/QC 熟悉 BDD、API Contract、整體系統互動，有能力檢查規格無歧義性
3. **流程連貫性**：QA/QC 既審查規格（Gate 1），又驗證實作（Gate 2），能確保「所說 = 所做」

### 與既有流程（ADR-001）的相容性

- ✅ 不破壞「平行施工」原則（仍是前端 / 後端 / DBA 並行）
- ✅ 不增加 Agent 數量（仍是 7 個 Agent）
- ✅ 與「批判迴圈」一致（QA/QC 審查後退回修正）
- ✅ 與「禁止路由」相容（Orchestrator 不直接指派 Agent，而是觸發閘門）

## 實作計畫

### 產出物
1. **更新 `.github/AGENTS.md`**
   - 更新工作流程圖（插入 Gate 1）
   - 新增「QA/QC 藍圖審查規則」章節
   - 更新「禁止路由」規則

2. **更新 `.github/agents/qa-qc.agent.md`**
   - 新增「藍圖規格審查職責」章節
   - 審查清單與退回機制

3. **更新 `.github/agents/orchestrator.agent.md`**
   - 新增「觸發 QA/QC 藍圖審查」步驟
   - 「等待審查」狀態機制

### 對現有 Agent 的影響
- **SA/SD**：需遵守 Frozen Contract 聲明、Handoff Contract 規範
- **前端 PG / 後端 PG / DBA**：不受影響（仍在 Gate 1 通過後開工）
- **E2E Test**：不受影響

## 風險與緩解

| 風險 | 影響 | 緩解策略 |
|------|------|---------|
| Gate 1 審查耗時 | 延遲並行施工開始 | 建立 SLA（24 小時內完成審查） |
| QA/QC 審查能力不足 | 漏掉設計缺陷 | 提供清晰的審查清單 + 定期 QA/QC training |
| SA/SD 抗拒增加約束 | 流程阻力 | 強調「規格品質 = 下游效率」 |

## 後續監控指標

1. **藍圖審查通過率**：目標 > 90%（需少於 1 次退回）
2. **審查耗時**：目標 < 2 工作天（不堵塞並行施工）
3. **後續 Gate 2 缺陷率**：應下降（若規格品質提高）

## 相關文件

- **ADR-001**：多智能體工作流程框架
- **`.github/AGENTS.md`**：工作流程與 Agent 職責
- **`.github/skills/bdd-conventions/SKILL.md`**：BDD 格式規範
- **`.github/skills/agent-handoff-contract/SKILL.md`**：Handoff Contract 範本

---

**決策版本**：v1.0  
**最後更新**：2026-04-26  
**審批狀態**：待 Orchestrator / 人類確認
