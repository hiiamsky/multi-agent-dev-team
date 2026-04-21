---
name: git-conventions
description: Git commit message format, branch naming strategy, and GitHub Flow rules for all agents. Use when writing any commit, creating a branch, or producing a PR. Automatically load this skill before any git operation.
when_to_use: 任何 Agent 準備 commit、建立 branch、產出 PR 描述、或需要確認 Git 操作規範時
---

# Git 規範（跨 Agent 共用）

本 skill 是本 multi-agent 團隊 Git 操作的**唯一真理來源**。
所有 Agent 在產生 commit、建立 branch、撰寫 PR 前，必須依本 skill 操作。

---

## 一、Commit Message 格式

```
TYPE: SUBJECT

BODY

FOOTER
```

- 標題列（第一行）必須包含 **TYPE** 與 **SUBJECT**，以冒號加空格分隔
- BODY 與 FOOTER 各以空行隔開，非必填
- **FOOTER 必填**，至少包含 `issue #N`

---

## 二、TYPE 類型

| TYPE | 說明 | 影響程式碼 |
|------|------|-----------|
| `Feat` | 新功能 | ✓ |
| `Modify` | 既有功能需求調整的修改 | ✓ |
| `Fix` | 錯誤修正 | ✓ |
| `Docs` | 更新文件（README、AGENTS.md、ADR、SKILL 等） | ✗ |
| `Style` | 程式碼格式調整（formatting、缺少分號等） | ✗ |
| `Refactor` | 重構，不改變既有邏輯 | ✓ |
| `Test` | 新增或重構測試 | ✗ |
| `Chore` | 更新專案建置設定、更新版本號等瑣事 | ✗ |
| `Revert` | 撤銷之前的 commit | ✓ |

**Revert 格式**：`Revert: TYPE: SUBJECT (回覆版本：xxxx)`

---

## 三、SUBJECT 主旨規則

- **不超過 50 個字元**
- 英文大寫開頭，中英文都**不用句號**結尾
- 以**祈使句**書寫：`Add`、`Fix`、`Update`、`Remove`
- 言簡意賅，一句話說清楚「做了什麼」

---

## 四、BODY 本文規則（非必填）

若撰寫，須說明：
- **改了什麼**（What）
- **為什麼而改**（Why）
- 每行不超過 72 個字

---

## 五、FOOTER 頁尾規則（必填）

**基本格式**（所有 commit 必須包含）：
```
issue #N
```

**含架構決策時加入**（Orchestrator 責任）：
```
issue #N
ADR: docs/specs/adr/ADR-XXX-{short-name}.md
```

**影響下游 Agent 決策時加入**：
```
issue #N
ADR: docs/specs/adr/ADR-XXX-{short-name}.md
⚠️ MUST-READ
```

> `⚠️ MUST-READ` 旗標會被 Orchestrator 在階段零 ADR 查詢時透過
> `git log --grep="MUST-READ" --oneline` 檢出，通知所有 Agent 必看。
> **觸發條件**：任何影響下游 Agent 行為的架構決策、規則變更、介面凍結。

---

## 六、Commit 範例

### 一般 Fix（必填 FOOTER）
```
Fix: 修正首頁資料載入緩慢問題

- 首頁載入後等待超過 10 秒資料才顯示
- 將資料改為一次撈取，並暫存在記憶體中

issue #456
```

### 含 ADR 的 Feat
```
Feat: 新增 Redis 草稿快取機制

- 草稿 TTL < 24h，不需持久化至 PostgreSQL
- 減少 DB 寫入壓力約 60%

issue #123
ADR: docs/specs/adr/ADR-007-cache-strategy.md
⚠️ MUST-READ
```

### Docs（L1 任務常見格式）
```
Docs: 更新 AGENTS.md 任務分級 L1 範疇說明

issue #89
```

### Agent 系統變更（新增 agent 檔案時）
```
Feat: 新增 data-analyst agent 角色

- 依 SA/SD 藍圖新增 data-analyst.agent.md
- 工具白名單：codebase, search（唯讀，無 editFiles）

issue #201
ADR: docs/specs/adr/ADR-012-data-analyst-agent.md
⚠️ MUST-READ
```

---

## 七、分支命名規則

**格式**：`{prefix}/{issue-no}-{short-name}`

| 前綴 | 用途 | 範例 |
|------|------|------|
| `feature/` | 新功能 | `feature/42-user-login` |
| `fix/` | 錯誤修正 | `fix/57-login-timeout` |
| `refactor/` | 重構 | `refactor/63-auth-module` |
| `docs/` | 文件更新 | `docs/70-api-spec` |

**命名規則**：
- 必須包含 GitHub Issue 編號
- `{short-name}` 使用小寫英文與連字號，不用底線
- 不超過 50 個字元（含前綴與 Issue 編號）

---

## 八、GitHub Flow 規則

```
main（永遠可部署）
  └─ feature/{N}-{name}   ← 開工即開分支
       ├─ SA/SD commit
       ├─ 實作層 commit（各 worktree merge 後）
       └─ QA/QC 通過 → PR → 人類批准 → merge → 刪除分支
```

**四條硬性規則**：

1. **`main` 永遠可部署**，禁止直接 push
2. **開工即開分支**，任務從 main 切出
3. **QA/QC 通過才合併**，feature branch 必須有「可發布」標記
4. **合併後刪除分支**，保持 repo 乾淨

---

## 九、PR 描述格式

PR 描述由 Orchestrator 負責撰寫，必須包含：

```markdown
## 功能摘要
{一段話說明本次 PR 做了什麼}

## 涉及的 Agent 產出
- SA/SD：docs/specs/{feature-name}-spec.md
- 後端 PG：src/backend/...
- 前端 PG：src/frontend/...
- DBA：db/migrations/{nnnn}_{description}.sql
- QA/QC：docs/reviews/{feature-name}-review.md

## QA/QC 驗證結果
- 結果：✅ 可發布 / ❌ 退回修正
- 總檢查項：N，通過：N，偏差：N

## 安全驗證摘要
- Critical / High 缺陷：N（阻擋：是/否）
- Medium / Low 缺陷：N
- 禁止豁免命中：有/無

## 本次新增 / 引用的 ADR
- ADR: docs/specs/adr/ADR-XXX-...（若有）

closes #N
```

---

## 十、禁止事項

- ❌ 直接 commit 到 `main`
- ❌ 在未經 QA/QC 驗證的情況下合併 PR
- ❌ 保留已合併的 feature branch
- ❌ FOOTER 缺少 `issue #N`
- ❌ 含架構決策的 commit 未附 `ADR:` 引用
- ❌ 影響下游 Agent 行為的變更未加 `⚠️ MUST-READ`
- ❌ SUBJECT 超過 50 字元或以句號結尾