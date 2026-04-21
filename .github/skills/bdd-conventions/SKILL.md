---
name: bdd-conventions
description: BDD User Story and Scenario format conventions including Given/When/Then structure, SC-XX-YY numbering, Frozen Contract declaration, and API Contract derivation rules. Use when SA/SD is writing BDD User Stories or when E2E Test is mapping test blocks to BDD Scenarios.
when_to_use: SA/SD 產出 BDD User Stories 時、E2E 測試 Agent 撰寫 Playwright test block 時、QA/QC 驗證 BDD Scenario 覆蓋率時
---

# BDD 規範（跨 Agent 共用）

本 skill 是本 multi-agent 團隊 BDD User Stories 的**唯一格式標準**。

**三個 Agent 必須對齊同一套規範**：
- **SA/SD**：產出 BDD User Stories（寫入 `docs/specs/{feature}-spec.md`）
- **E2E Test**：以 BDD Scenario 為 test block 骨架（寫入 `tests/e2e/{feature}.spec.ts`）
- **QA/QC**：驗證 BDD Scenario 100% 覆蓋率

---

## 一、Story 格式

```
## Story {SC-XX}：{故事標題}
As a {角色}
I want to {動作}
So that {業務價值}
```

**規則**：
- `{SC-XX}` 為二位數流水號，從 `SC-01` 開始
- 角色使用業務語言（`商家`、`顧客`、`管理員`），不用技術用語（`User`）
- `So that` 必須能對應到 Orchestrator 精煉需求中的核心業務痛點

---

## 二、Scenario 格式

```
### Scenario {SC-XX-YY}：{情境標題}
Given {前置條件}
When  {觸發動作}
Then  {預期結果}
```

**編號規則**：
- `{SC-XX-YY}` — 前兩位對應 Story，後兩位為情境流水號
- 每個 Story 至少兩個 Scenario：`01` Happy Path、`02` 異常/邊界
- 範例：`SC-01-01`（Story 1 的第一個情境）、`SC-01-02`（Story 1 的第二個情境）

**Then 的必填內容**：

| Scenario 類型 | Then 必須包含 |
|-------------|-------------|
| Happy Path | UI 顯示欄位清單（每個欄位逐一列出）+ HTTP 200 |
| 異常 / 邊界 | HTTP status code（409、422、401 等）+ 錯誤訊息格式 |

> **Then 的欄位清單是 API Contract 的唯一推導來源**，不得腦補額外欄位。

---

## 三、完整 BDD User Story 範例

```markdown
## Story SC-01：菜商發布今日菜單

As a 菜商
I want to 將草稿菜單發布為今日供應菜單
So that 顧客能看到今日可訂購的品項與售價

### Scenario SC-01-01：正常發布
Given 草稿菜單中有 3 個品項，且每個品項的名稱、售價、庫存數量皆已填寫
When  菜商點擊「發布」按鈕
Then  API 回傳 HTTP 200
      畫面顯示：品項名稱、售價、庫存數量、發布時間

### Scenario SC-01-02：草稿有未填寫欄位
Given 草稿菜單中有 1 個品項未填寫售價
When  菜商點擊「發布」按鈕
Then  API 回傳 HTTP 422
      畫面顯示錯誤訊息：「請填寫所有品項的售價後再發布」

## Story SC-02：顧客訂購品項

As a 顧客
I want to 選擇品項並送出訂單
So that 我能在指定時間收到預訂的餐點

### Scenario SC-02-01：正常訂購
Given 某品項剩餘庫存為 5
When  顧客訂購數量 2
Then  API 回傳 HTTP 200
      畫面顯示：訂單編號、品項名稱、數量、總金額、預計取餐時間

### Scenario SC-02-02：庫存不足
Given 某品項剩餘庫存為 0
When  顧客嘗試訂購數量 1
Then  API 回傳 HTTP 409
      畫面顯示錯誤訊息：「庫存不足，無法訂購」
      remaining_qty 維持 0，不被扣減
```

---

## 四、API Contract 推導規則（SA/SD 專用）

**從 BDD Then → API Contract，不得反向或腦補**：

| BDD 元素 | 對應 API Contract |
|---------|-----------------|
| `Then` 中的 UI 欄位清單 | Response DTO 的必要欄位 |
| `When` 中描述的操作 | HTTP Method + 路由 + Request payload |
| `Then` 中的 HTTP status code | API 完整狀態碼覆蓋 |

**範例推導**（從上方 SC-01-01）：

```yaml
# Then 說: 畫面顯示 品項名稱、售價、庫存數量、發布時間
# 推導 →
POST /api/menus/publish
Response 200:
  {
    "items": [
      {
        "name": string,      # ← 品項名稱
        "price": number,     # ← 售價
        "stock": integer,    # ← 庫存數量
        "published_at": datetime  # ← 發布時間
      }
    ]
  }

# Then 說: API 回傳 HTTP 422 + 錯誤訊息
# 推導 →
Response 422:
  {
    "error": {
      "code": "VALIDATION_FAILED",
      "message": "請填寫所有品項的售價後再發布"
    }
  }
```

---

## 五、Frozen Contract 聲明格式

SA/SD 藍圖文件的**標題下方**必須加入聲明：

```markdown
> ⚠️ API Contract v{版本號}：本藍圖中的 API 規格由 BDD Scenarios 推導，
> 任何變更須退回本階段重新推導並升版。
```

**版本號規則**：
- 初版為 `v1.0`
- 任何 API 欄位變更（增減欄位、改型別、改狀態碼）必須升版

---

## 六、BDD User Stories 章節位置

SA/SD 產出的規格文件結構：

```markdown
# {功能名稱} 規格藍圖

> ⚠️ API Contract v1.0：...（Frozen Contract 聲明）

## BDD User Stories    ← 必須在文件頂部第一個章節

（所有 Story + Scenario 內容）

## 系統邊界定義
...
## API 規格
...
```

---

## 七、E2E Test block 對應規則

E2E Test Agent 必須以 BDD Scenario 為 test block 骨架，**1:1 對應**：

```typescript
// SC-01-01 → 一個 test block
test('[SC-01-01] 正常發布', async ({ page }) => {
  // Given: 草稿菜單中有 3 個品項，且每個品項的名稱、售價、庫存數量皆已填寫
  // When: 菜商點擊「發布」按鈕
  // Then: 斷言 API 回傳 200，畫面顯示品項名稱、售價、庫存數量、發布時間
});

// SC-01-02 → 一個 test block
test('[SC-01-02] 草稿有未填寫欄位', async ({ page }) => {
  // Given: 草稿菜單中有 1 個品項未填寫售價
  // When: 菜商點擊「發布」按鈕
  // Then: 斷言 API 回傳 422，畫面顯示錯誤訊息
});
```

**test 描述格式**：`[{SC-XX-YY}] {Scenario 標題}`（方括號內為 Scenario ID）

---

## 八、QA/QC 覆蓋率驗證規則

- **100% 覆蓋率**：藍圖中有幾個 Scenario，E2E Test 就必須有幾個 test block
- 任何未覆蓋的 Scenario = 缺陷，退回 E2E Test Agent
- 實作的 API Response 欄位與 BDD Then 推導的 Frozen Contract 不一致 = 缺陷，退回後端 PG

---

## 九、常見錯誤

| 錯誤 | 正確做法 |
|------|---------|
| Then 未列出具體 UI 欄位 | 逐一列出畫面顯示的每個欄位 |
| Then 未包含 HTTP status code | 每個 Scenario 必須有明確的狀態碼 |
| API Contract 多了 Then 沒有的欄位 | 刪除，Contract 只能從 Then 推導 |
| E2E test 自行新增無 Scenario 對應的 test | 刪除，只能 1:1 對應 |
| SC 編號跳號（SC-01 後直接 SC-03） | 從 SC-01 開始連續編號，不跳號 |
| Story 的 So that 是技術描述 | 改用業務價值描述（使用者能得到什麼） |