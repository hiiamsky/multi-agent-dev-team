---
name: agent-handoff-contract
description: Agent Handoff Contract template, required fields definition, and acceptance criteria. Use when SA/SD is producing a specification blueprint, when Orchestrator is verifying blueprint completeness, or when QA/QC is checking whether the blueprint contains this mandatory section.
when_to_use: SA/SD 產出規格藍圖時（必須在藍圖底部加入此章節）、Orchestrator 驗收 SA/SD 交付物時、QA/QC 執行「規格與產出對齊」階段的第一步驗證時
---

# Agent Handoff Contract（跨 Agent 共用）

本 skill 定義 Agent Handoff Contract 的**標準模板與必填規則**。

**為何存在**：SA/SD 的規格藍圖是下游三個 Agent（前端 PG / 後端 PG / DBA）並行施工的起點。Contract 是確保並行不失控的「交接清單」——缺少它，下游 Agent 只能猜測上游的前提假設，是系統級缺陷的主要來源之一。

**強制規則**：
- SA/SD 每份規格藍圖**必須包含** `## Agent Handoff Contract` 章節
- QA/QC 在「規格與產出對齊（Artifact Alignment）」階段，**第一步**就是驗證此章節是否存在
- 缺少此章節 → QA/QC **直接退回 SA/SD**，不進入後續驗證（Critical 缺陷）

---

## 一、標準模板

以下為完整的 `## Agent Handoff Contract` 章節模板，
SA/SD 產出藍圖時必須依此格式填寫於文件**最底部**：

```markdown
## Agent Handoff Contract

> ⚠️ 此章節為強制欄位。缺少此章節，Orchestrator 將退回本藍圖。

### 前提假設（下游 Agent 不得違反）

- （列出下游實作必須遵守的架構前提，例如：
  欄位格式、資料結構、TTL 設定、加密策略、冪等性設計）

### 架構決策記錄

| 決策主題 | 選擇方案 | 被拒絕方案 | 拒絕理由 |
|---------|---------|-----------|---------|
| （範例）存儲層 | Redis | PostgreSQL | 草稿 TTL < 24h，不需持久化 |

### ADR 引用

- （若有新建 ADR，列出連結；若無架構決策，填「無」）
- 範例：`ADR: docs/specs/adr/ADR-007-cache-strategy.md`

### 給各 Agent 的提醒

#### backend-pg 注意
- （哪些介面已凍結，不能更改）
- （哪些業務規則屬於 Domain 層，不能放在 Controller）
- （安全設計的關鍵約束：加密欄位、授權檢查位置）

#### frontend-pg 注意
- （API 回傳格式的特殊設計，如分頁結構、巢狀物件）
- （哪些欄位預設遮蔽，需主動請求完整版）
- （Token 儲存與刷新策略）

#### DBA 注意
- （Schema 設計的關鍵約束：型別精確定義、索引必要性）
- （敏感欄位的加密/雜湊要求）
- （稽核欄位的必填規定）
```

---

## 二、各欄位填寫說明

### 前提假設

列出下游 Agent **必須遵守、不得自行推翻**的假設。若下游 Agent 發現假設有問題，必須退回 SA/SD，不得自行修改。

**典型範例**：

```markdown
### 前提假設
- 所有時間欄位使用 UTC 時區，格式為 ISO 8601（`2025-04-19T07:30:00Z`）
- 分頁採用 cursor-based pagination，非 offset-based
- 身分證字號欄位在 DB 以 AES-256-GCM 加密儲存，API 回傳遮蔽為 `A12****789`
- 訂單金額計算以分（cents）為單位，避免浮點數誤差
- 所有端點預設需認證（JWT Bearer），明確標註 `[AllowAnonymous]` 者除外
```

### 架構決策記錄

記錄 SA/SD 在設計過程中「選了什麼、拒絕了什麼、為什麼」。
目的是讓下游 Agent 不必重新思考已決策的問題，也避免誤以為「還有選擇空間」。

**填寫提示**：
- 只記錄**有意識做了選擇的決策**，不需把所有設計都列入
- 若無任何架構決策，填「無」即可（但這通常代表需求太簡單，需再確認）

### ADR 引用

若本次設計包含新的重要架構決策（技術選型、模式選擇、效能取捨），必須：

1. 建立 ADR 文件：`docs/specs/adr/ADR-{NNN}-{short-name}.md`
2. 在此處列出連結

**觸發 ADR 的典型情境**：
- 引入新的基礎設施（Redis、Message Queue、CDN）
- 選擇特定設計模式（CQRS、Event Sourcing、Saga）
- 效能優化決策（Denormalization、Caching 策略）
- 安全架構決策（認證機制、加密方案）

### 給各 Agent 的提醒

這是 Handoff Contract 中**最重要的一節**，直接降低跨域檢視的摩擦。

**填寫原則**：
- 只寫下游 Agent **從藍圖其他章節讀不出來**的資訊
- 以「會踩的坑」和「容易誤解的地方」為主
- 每個 Agent 只寫與它相關的內容，不要一視同仁複製相同內容

---

## 三、Orchestrator 驗收標準

Orchestrator 收到 SA/SD 藍圖後，驗收 Handoff Contract 的最低標準：

| 驗收項目 | 通過條件 | 不通過處理 |
|---------|---------|-----------|
| 章節存在 | 藍圖底部有 `## Agent Handoff Contract` | 退回 SA/SD，不分派實作任務 |
| 前提假設非空 | 至少有 1 條假設 | 退回 SA/SD 補充 |
| 架構決策有回應 | 有填寫「無」或具體決策 | 退回 SA/SD 補充 |
| 給 Agent 的提醒已針對性填寫 | 至少有 backend-pg 與 DBA 的提醒 | 退回 SA/SD 補充 |
| 若有新 ADR → ADR 文件已建立 | `docs/specs/adr/ADR-NNN-*.md` 存在 | 退回 SA/SD 建立 |

---

## 四、QA/QC 驗證規則

QA/QC 在「規格與產出對齊（Artifact Alignment）」階段：

1. **第一步**：確認 `## Agent Handoff Contract` 章節存在
   - 不存在 → **立即退回 SA/SD**，嚴重度 Critical，不進入後續驗證
   - 存在 → 繼續後續驗證

2. **驗證前提假設是否被下游遵守**：
   - 前提假設中的欄位格式 → 對照後端 DTO 與前端 TypeScript Interface
   - 加密/遮蔽策略 → 對照 DBA Schema 型別與前端顯示邏輯
   - 若前提假設被違反 → 退回對應 Agent，非退回 SA/SD

3. **驗證 ADR 引用完整性**：
   - 若有 `ADR:` 引用 → 確認 ADR 文件實際存在
   - 若 commit 訊息含新架構決策但未附 `ADR:` 引用 → Low 缺陷，退回 Orchestrator 補充

---

## 五、完整填寫範例

以「商家發布菜單」功能為例：

```markdown
## Agent Handoff Contract

> ⚠️ 此章節為強制欄位。缺少此章節，Orchestrator 將退回本藍圖。

### 前提假設（下游 Agent 不得違反）

- 發布時間使用 UTC 時區，格式 ISO 8601：`published_at: "2025-04-19T07:30:00Z"`
- 庫存數量為整數（integer），不接受小數點
- 品項售價以整數分（cents）儲存，API 回傳時除以 100 呈現為元
- 菜單發布為**不可逆操作**，發布後只能下架（soft delete），不能直接刪除
- 所有菜單 API 端點需認證（JWT Bearer），菜商角色（`role: merchant`）

### 架構決策記錄

| 決策主題 | 選擇方案 | 被拒絕方案 | 拒絕理由 |
|---------|---------|-----------|---------|
| 草稿儲存 | Redis（TTL 24h） | PostgreSQL | 草稿不需持久化，Redis 讀寫效能優 |
| 庫存扣減 | DB 悲觀鎖（SELECT FOR UPDATE）| 樂觀鎖 | 訂購高併發場景，悲觀鎖更安全 |
| 菜單 ID 格式 | UUID v4 | 自增 INT | 防止 IDOR 攻擊，不暴露數量 |

### ADR 引用

- ADR: docs/specs/adr/ADR-008-menu-draft-cache.md

### 給各 Agent 的提醒

#### backend-pg 注意
- 庫存扣減使用 `SELECT FOR UPDATE`，必須在 Transaction 內執行
- 發布 API 需驗證 `currentUserId == menu.merchantId`（防 BOLA）
- `published_at` 由後端產生（`DateTime.UtcNow`），不接受前端傳入

#### frontend-pg 注意
- 售價欄位 API 回傳為 integer cents（如 `1500`），前端顯示時除以 100（`$15.00`）
- 草稿狀態 API 路徑為 `/api/menus/draft`（Redis），已發布為 `/api/menus`（DB）
- 發布按鈕點擊後需有二次確認對話框（不可逆操作）

#### DBA 注意
- `menus` 表需 `status` 欄位（`draft / published / archived`），不使用硬刪除
- `price` 欄位型別使用 `INT`（cents），不用 `DECIMAL`
- 需建立 `(merchant_id, status, published_at)` 複合索引，對應高頻查詢場景
```

---

## 六、常見錯誤

| 錯誤 | 正確做法 |
|------|---------|
| 章節放在藍圖中間 | 必須在文件**最底部** |
| 給各 Agent 的提醒三份一樣 | 依各 Agent 的職責針對性填寫 |
| 前提假設寫技術實作細節 | 前提假設是約束，實作細節在 API 規格章節 |
| 有新技術選型但 ADR 填「無」 | 有決策就必須建 ADR，填「無」代表無決策 |
| 依賴 Agent 自己去讀其他章節推導 | 把容易誤解的資訊**主動寫出來** |