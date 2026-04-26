# P4-001：整合測試種子資料與真實資料層驗證 — SA/SD 規格藍圖

> ⚠️ **API Contract v1.0**：本藍圖中的 API 規格由 BDD Scenarios 推導，任何變更須退回本階段重新推導並升版。
>
> 本任務**不新增任何 API endpoint**。所有 API 規格引用均為現有端點（P3-002 已凍結）。

---

## 文件資訊

| 項目 | 值 |
|------|---|
| Issue | #39 |
| Phase | 4 |
| Feature Branch | `feature/39-p4-001-seed-integration-test` |
| 上游輸入 | P3-002 已凍結 API Contract；現有 `VeggieAlly/tests/e2e/` 測試結構 |
| 前置完成 | P3-001（Redis 草稿管理）、P3-002（發布 + 庫存扣除 + 查詢 API） |
| 下游消費者 | DBA（Seed SQL）、E2E Test Agent（Playwright 測試案例） |
| 安全標籤 | ☑ 涉及不可逆操作（測試資料寫入真實 DB） |
| 狀態 | Draft — 待 QA/QC Gate 1 審查 |

---

## ADR Pre-Check 結果

| ADR | 狀態 | 相容性檢查 |
|-----|------|----------|
| ADR-001：漸進式工作流，Phase 1 Human-in-the-Loop | 已接受 | ✅ 本任務為 L2 標準任務，Orchestrator 直接派工，無衝突 |
| ADR-002：QA/QC 藍圖審查閘門 | 已提案 | ✅ 本藍圖 commit 後必須等待 QA/QC Gate 1 通過，才能開工 |

**本次設計無新架構決策需建立 ADR**（測試基礎建設屬於已知邊界內的新功能迭代）。

---

## BDD User Stories

### Feature：整合測試種子資料與真實資料層驗證

> 覆蓋範圍：Seed SQL 植入 → Redis 草稿模擬 → 菜單發布（DB 寫入）→ 庫存扣除 → 菜單查詢

---

### Story SC-39-01：測試環境草稿模擬植入

```
As a  自動化測試腳本
I want to  在 E2E 測試環境中直接將結構正確的草稿 JSON 植入 Redis
So that   後續的 POST /api/menu/publish 可以使用真實草稿資料完成全流程驗證
          （草稿建立本身由 LINE Webhook + LLM 驅動，無法在 E2E 環境重現，採測試替身）
```

#### Scenario SC-39-01-01：草稿植入 Redis 成功（Happy Path）

```gherkin
Given Redis 服務可連線（host: localhost, port: 6379）
  AND 當日不存在 key "e2e-test-tenant:draft:Ue2e000000000000000000000000test:{today}"
When  測試輔助函式 seedDraftToRedis() 將 DraftMenuSession JSON 寫入 Redis
  AND key 格式為 "{tenantId}:draft:{lineUserId}:{yyyy-MM-dd}"（台灣時區當日）
  AND JSON 採 snake_case 命名（JsonNamingPolicy.SnakeCaseLower）
  AND TTL 設定為 86400 秒（24 小時）
Then  Redis.EXISTS 該 key 回傳 1
  AND Redis.TTL 該 key 回傳值 > 0 且 ≤ 86400
  AND JSON 中 "items" 陣列長度 ≥ 1
  AND 每個 item 的 "validation.status" = "ok"
```

#### Scenario SC-39-01-02：草稿 JSON 格式不符，後端反序列化失敗（異常）

```gherkin
Given Redis 中已存在 key，但其值為非 DraftMenuSession 結構的 JSON（空物件 {}）
When  測試呼叫 POST /api/menu/publish（mock auth headers）
Then  回應 HTTP 500 或 404（後端無法解析草稿，視後端錯誤處理而定）
  AND DB published_menus 中無新增記錄（tenant_id = "e2e-test-tenant" AND date = today）
```

---

### Story SC-39-02：菜單發布 — 草稿寫入 PostgreSQL

```
As a  菜商操作人員（透過 LINE Postback 觸發，E2E 環境以 mock auth 替代）
I want to  呼叫 POST /api/menu/publish 將 Redis 草稿持久化至 PostgreSQL
So that   今日菜單正式對外提供，並可透過 GET /api/menu/today 查詢
```

#### Scenario SC-39-02-01：發布成功，PostgreSQL 寫入正確（Happy Path）

```gherkin
Given Redis 中存在有效草稿（SC-39-01-01 結果）
  AND published_menus 中不存在 tenant_id = "e2e-test-tenant" AND date = today 的記錄
When  POST /api/menu/publish（headers: X-Test-Auth:true, X-Test-TenantId:e2e-test-tenant, X-Test-LineUserId:Ue2e000000000000000000000000test）
Then  HTTP 201 Created
  AND response body 包含：
      - id: string（32 hex chars）
      - tenant_id: "e2e-test-tenant"
      - date: today（yyyy-MM-dd，台灣時區）
      - published_at: ISO 8601 timestamp
      - items: 陣列，長度等於草稿 items 數量
      - 每個 item 包含 id, name, sell_price, original_qty, remaining_qty, unit
  AND DB 驗證（直接查詢 PostgreSQL）：
      - published_menus 中存在一筆記錄（tenant_id = "e2e-test-tenant", date = today）
      - published_menu_items 中存在對應品項記錄
      - 每個 item 的 remaining_qty = original_qty（發布時庫存尚未扣除）
  AND Redis cache key "e2e-test-tenant:menu:published:{today}" 存在（write-through）
  AND Redis draft key "e2e-test-tenant:draft:Ue2e000000000000000000000000test:{today}" 已被刪除（發布後草稿清除）
```

#### Scenario SC-39-02-02：重複發布同日菜單，回 409（異常）

```gherkin
Given published_menus 中已存在 tenant_id = "e2e-test-tenant" AND date = today 的記錄
  AND Redis cache key "e2e-test-tenant:menu:published:{today}" 存在
When  再次 POST /api/menu/publish（相同 mock auth headers）
Then  HTTP 409 Conflict
  AND response body 包含 error.code = "MENU_ALREADY_PUBLISHED"（或語義等價欄位）
  AND DB published_menus 中仍只有一筆記錄（不重複插入）
```

---

### Story SC-39-03：庫存扣除 — 更新 PostgreSQL remaining_qty

```
As a  LIFF 消費者端
I want to  選購品項後扣除庫存
So that   remaining_qty 正確反映剩餘可購量，並持久化至 PostgreSQL
```

#### Scenario SC-39-03-01：庫存足夠，扣除後 DB 正確遞減（Happy Path）

```gherkin
Given published_menu_items 中品項 SEED_ITEM_1_ID 的 remaining_qty = 10
When  PATCH /api/menu/inventory
      body: { "item_id": "{SEED_ITEM_1_ID}", "amount": 3 }
      headers: mock auth（e2e-test-tenant）
Then  HTTP 200 OK
  AND response body 包含：
      - id: SEED_ITEM_1_ID
      - remaining_qty: 7（= 10 - 3）
      - name: "E2E 測試青菜"
      - unit: "斤"
  AND DB 直接驗證：
      published_menu_items.remaining_qty WHERE id = SEED_ITEM_1_ID = 7
```

#### Scenario SC-39-03-02：庫存不足，DB 保持不變，回 409（異常）

```gherkin
Given published_menu_items 中品項 SEED_ITEM_2_ID 的 remaining_qty = 2
When  PATCH /api/menu/inventory
      body: { "item_id": "{SEED_ITEM_2_ID}", "amount": 999 }
      headers: mock auth（e2e-test-tenant）
Then  HTTP 409 Conflict
  AND response body 包含 error.code = "INSUFFICIENT_STOCK"（或語義等價欄位）
  AND DB 直接驗證：
      published_menu_items.remaining_qty WHERE id = SEED_ITEM_2_ID = 2（不變）
```

---

### Story SC-39-04：查詢已發布菜單 — 驗證 PostgreSQL 讀取正確性

```
As a  LIFF 消費者端
I want to  查詢今日已發布菜單
So that   看到完整品項清單與即時庫存，資料確實來自 PostgreSQL（非 mock）
```

#### Scenario SC-39-04-01：今日有菜單，回傳完整品項（Happy Path）

```gherkin
Given published_menus 中存在 tenant_id = "e2e-test-tenant" AND date = today 的記錄
  AND published_menu_items 中存在對應的 2 個品項（SEED_ITEM_1_ID, SEED_ITEM_2_ID）
  AND Redis cache key "e2e-test-tenant:menu:published:{today}" 已清除（確保走 DB 路徑）
When  GET /api/menu/today（headers: mock auth, tenantId = "e2e-test-tenant"）
Then  HTTP 200 OK
  AND response body 包含：
      - tenant_id: "e2e-test-tenant"
      - date: today（yyyy-MM-dd）
      - items: 陣列長度 ≥ 2
      - items 中存在 id = SEED_ITEM_1_ID，remaining_qty = 10，unit = "斤"
      - items 中存在 id = SEED_ITEM_2_ID，remaining_qty = 2，unit = "顆"
```

#### Scenario SC-39-04-02：無今日菜單，回 404（異常）

```gherkin
Given published_menus 中不存在 tenant_id = "e2e-no-menu-tenant" AND date = today 的記錄
  AND Redis cache key "e2e-no-menu-tenant:menu:published:{today}" 不存在
When  GET /api/menu/today（headers: mock auth, tenantId = "e2e-no-menu-tenant"）
Then  HTTP 404 Not Found
  AND response body 包含 error.code = "MENU_NOT_FOUND"（或語義等價欄位）
```

---

## 系統邊界定義

| 層次 | 職責範圍 |
|------|---------|
| **DBA Agent** | 建立 `db/seeds/` 目錄；撰寫 `001_e2e_test_seed.sql`（idempotent，覆蓋 `e2e-seed-tenant` 靜態基準資料）；更新 `docker-compose.test.yml` 的 `db-init` 指令加入 seed 執行步驟與 volume mount |
| **E2E Test Agent** | 新增 `tests/e2e/tests/publish-flow.spec.ts`（完整真實資料流：草稿植入 Redis → 發布 → DB 驗證 → 查詢 → 庫存扣除 → DB 最終驗證）；在 `helpers/db.ts` 新增 `seedDraftToRedis()` 與 `clearPublishedMenusForTenant()` 輔助函式 |
| **前端 PG** | 不涉及 |
| **後端 PG** | 不涉及（不修改任何後端程式碼） |
| **資料層職責** | PostgreSQL：`published_menus` + `published_menu_items` 表（已存在）；Redis：Draft key + Published cache key（已存在）；Seed SQL 僅插入靜態參考資料，不影響 migration |

**明確排除**：
- 不新增任何 API endpoint
- 不修改 backend C# 程式碼
- 不涉及生產環境資料初始化

---

## 時序邏輯

### 完整真實資料流（新 E2E 測試案例 `publish-flow.spec.ts` 執行路徑）

```
beforeEach (clearPublishedMenusForTenant + seedDraftToRedis)
    │
    ├─ DELETE FROM published_menus WHERE tenant_id LIKE 'e2e-%'   [PostgreSQL]
    ├─ DEL "e2e-test-tenant:menu:published:{today}"                [Redis]
    └─ SET "e2e-test-tenant:draft:Ue2e000000000000000000000000test:{today}" → DraftMenuSession JSON, TTL=86400  [Redis]

Test: Full Publish Flow
    │
    ├─① POST /api/menu/publish  (mock auth headers)
    │     └─ PublishMenuHandler:
    │           cache.ExistsAsync → false
    │           draftStore.GetAsync → DraftMenuSession
    │           repository.InsertAsync → DB WRITE ← 驗證點①
    │           cache.SetAsync → Redis WRITE
    │           draftStore.DeleteAsync → Redis DELETE
    │           → HTTP 201 + PublishedMenuDto
    │
    ├─② DB 直接驗證（node-postgres）
    │     SELECT * FROM published_menus WHERE tenant_id='e2e-test-tenant' AND date=today
    │     SELECT * FROM published_menu_items WHERE menu_id={menu_id}
    │     → assert: 行數 > 0，remaining_qty = original_qty  ← 驗證點②
    │
    ├─③ GET /api/menu/today  (mock auth headers)
    │     └─ GetTodayMenuHandler:
    │           cache.GetAsync → hit（write-through from ①）
    │           → HTTP 200 + PublishedMenuDto  ← 驗證點③
    │
    ├─④ PATCH /api/menu/inventory  (mock auth headers)
    │     body: { item_id: {first_item_id}, amount: 1 }
    │     └─ DeductInventoryHandler:
    │           UPDATE remaining_qty = remaining_qty - 1 WHERE remaining_qty >= 1
    │           → HTTP 200 + Updated PublishedMenuItem  ← 驗證點④
    │
    └─⑤ DB 直接驗證（node-postgres）
          SELECT remaining_qty FROM published_menu_items WHERE id = {first_item_id}
          → assert: remaining_qty = original_qty - 1  ← 驗證點⑤（最終業務狀態確認）
```

### Seed SQL 執行時序（容器啟動）

```
docker compose up
    │
    ├─ postgres: service_healthy（pg_isready 通過）
    │
    └─ db-init: one-shot container
          ├─ psql … -f /migrations/001_create_published_menus.sql
          ├─ psql … -f /migrations/002_ensure_published_menus_unique_constraint.sql
          └─ psql … -f /seeds/001_e2e_test_seed.sql       ← 新增
```

---

## Seed SQL 設計規格

### 概述

| 項目 | 規格 |
|------|------|
| 檔案路徑 | `VeggieAlly/db/seeds/001_e2e_test_seed.sql` |
| 冪等策略 | `DELETE … WHERE tenant_id = 'e2e-seed-tenant'` + `INSERT`（每次都從乾淨狀態重建） |
| 租戶區分 | `e2e-seed-tenant`（靜態基準資料，不被 `resetAndSeedTestData()` 的 `LIKE 'e2e-%'` 誤刪）⚠️ 見下方說明 |
| 測試日期 | 固定歷史日期 `'2020-01-01'`（不與台灣時區今日日期衝突） |
| 目的 | 驗證容器啟動後 schema 可寫入；提供靜態基準記錄供 CI 健康度快速判斷 |

> ⚠️ **設計說明**：`resetAndSeedTestData()` 刪除條件為 `tenant_id LIKE 'e2e-%'`，`e2e-seed-tenant` 符合此 pattern，**會被清除**。這是預期行為：靜態 seed 僅在容器啟動時驗證 schema 可寫入，per-test 重置後的動態資料由各測試的 `beforeEach` 自行管理。若日後需要跨測試保留靜態基準資料，可改用 `seed-tenant`（不含 `e2e-` 前綴）。目前 MVP 設計以簡單為優先，使用 `e2e-seed-tenant`。

### 固定識別碼規格

| 識別碼常數 | 值（32 hex chars GUID "N" 格式） | 說明 |
|-----------|--------------------------------|------|
| `SEED_STATIC_MENU_ID` | `beefcafe000000000000000000000001` | 靜態基準菜單 |
| `SEED_STATIC_ITEM_1_ID` | `beefcafe000000000000000000000101` | 靜態品項 1（高庫存） |
| `SEED_STATIC_ITEM_2_ID` | `beefcafe000000000000000000000102` | 靜態品項 2（低庫存） |

> 注意：以上 ID 為 Seed SQL 專用靜態資料。動態 E2E 測試使用的 `SEED_MENU_ID`（`deadbeef...`）、`SEED_ITEM_1_ID`（`deadbeef...101`）等由 `db.ts` 的 `resetAndSeedTestData()` 管理，兩者不互相干擾。

### 種子資料內容

**published_menus 資料（1 筆）**：

| 欄位 | 值 |
|------|---|
| id | `beefcafe000000000000000000000001` |
| tenant_id | `e2e-seed-tenant` |
| published_by | `Ue2e000000000000000000000000seed` |
| date | `2020-01-01` |
| published_at | `2020-01-01 00:00:00+00` |

**published_menu_items 資料（2 筆）**：

品項 1（高庫存）：

| 欄位 | 值 |
|------|---|
| id | `beefcafe000000000000000000000101` |
| menu_id | `beefcafe000000000000000000000001` |
| tenant_id | `e2e-seed-tenant` |
| name | `Seed 測試白菜` |
| is_new | `false` |
| buy_price | `80.00` |
| sell_price | `120.00` |
| original_qty | `20` |
| remaining_qty | `20` |
| unit | `斤` |
| historical_avg_price | `85.00` |

品項 2（低庫存）：

| 欄位 | 值 |
|------|---|
| id | `beefcafe000000000000000000000102` |
| menu_id | `beefcafe000000000000000000000001` |
| tenant_id | `e2e-seed-tenant` |
| name | `Seed 測試番茄` |
| is_new | `true` |
| buy_price | `60.00` |
| sell_price | `90.00` |
| original_qty | `3` |
| remaining_qty | `3` |
| unit | `盒` |
| historical_avg_price | `NULL` |

### Idempotent 策略詳述

執行順序：
1. `DELETE FROM published_menus WHERE tenant_id = 'e2e-seed-tenant'`（CASCADE 自動刪除 published_menu_items）
2. `INSERT INTO published_menus … VALUES (…)`
3. `INSERT INTO published_menu_items … VALUES (…), (…)`

**不使用 `ON CONFLICT DO NOTHING`** 的理由：靜態 seed 應每次重建為已知確定狀態，不應保留舊資料；DELETE + INSERT 語義更明確，確保欄位值一致性。

---

## docker-compose.test.yml 整合方案

### 變更項目

**新增 volume mount**（`db-init` 服務）：
```
./db/seeds:/seeds:ro
```

**新增 seed 執行步驟**（在現有 migration 指令之後）：
```
psql -h postgres -U veggie -d veggieally -f /seeds/001_e2e_test_seed.sql
```

完整 `db-init` command 更新後格式：

| 步驟 | 指令 | 說明 |
|------|------|------|
| 1 | `psql … -f /migrations/001_create_published_menus.sql` | 現有 — 建立 schema |
| 2 | `psql … -f /migrations/002_ensure_published_menus_unique_constraint.sql` | 現有 — 補強 constraint |
| **3** | **`psql … -f /seeds/001_e2e_test_seed.sql`** | **新增 — 植入靜態基準資料** |

**seed 執行時機**：migration 完成後、`veggie-ally` 服務啟動前（`db-init: condition: service_completed_successfully` 已確保此順序）。

**無需額外修改**：`veggie-ally` 服務的 `depends_on.db-init.condition: service_completed_successfully` 已確保 seed 在 API 服務啟動前完成。

---

## E2E 測試案例設計

### 新增檔案規格

| 項目 | 規格 |
|------|------|
| 新測試檔案 | `VeggieAlly/tests/e2e/tests/publish-flow.spec.ts` |
| 新輔助函式 | `helpers/db.ts` 中追加 `seedDraftToRedis()` 與 `clearPublishedMenusForTenant()` |
| 現有測試 | `tests/inventory.spec.ts` — **不修改**（現有 4 條測試繼續覆蓋 SC-39-03、SC-39-04） |

### BDD Scenario ↔ 測試案例對應表

| BDD Scenario | 測試檔案 | 測試函式描述 |
|-------------|---------|------------|
| SC-39-01-01 | `publish-flow.spec.ts` | `beforeEach` 中的 `seedDraftToRedis()` 執行驗證 |
| SC-39-01-02 | `publish-flow.spec.ts` | `Edge Case — 草稿 JSON 格式無效時 POST /publish 應返回 500/404` |
| SC-39-02-01 | `publish-flow.spec.ts` | `Happy Path — 完整真實資料流：草稿→發布→查詢→庫存扣除 DB 驗證` |
| SC-39-02-02 | `publish-flow.spec.ts` | `Edge Case — 重複發布應回 409 且 DB 記錄不變` |
| SC-39-03-01 | `inventory.spec.ts`（現有） | `Happy Path — PATCH /api/menu/inventory 扣除 3 單位後 remaining_qty 正確遞減` |
| SC-39-03-02 | `inventory.spec.ts`（現有） | `Edge Case — PATCH 超量請求應回 409，且 DB remaining_qty 不變` |
| SC-39-04-01 | `inventory.spec.ts`（現有） | `Happy Path — GET /api/menu/today 回傳 200 及品項陣列` |
| SC-39-04-02 | `inventory.spec.ts`（現有） | `Edge Case — 無菜單租戶的 GET /api/menu/today 應回 404` |

> **測試隔離設計原則**：`publish-flow.spec.ts` 的每個 test 使用獨立的 `beforeEach`，呼叫 `clearPublishedMenusForTenant('e2e-test-tenant')` + `seedDraftToRedis()` 確保乾淨起始狀態。`inventory.spec.ts` 維持既有 `resetAndSeedTestData()`（不修改）。

### seedDraftToRedis() 函式規格

**目的**：將符合 C# `DraftMenuSession` 序列化格式的 JSON 直接寫入 Redis，模擬 LINE Webhook + LLM 完成草稿建立後的狀態。

**Redis Key 格式**（來自 P3-001 §6.1）：
```
{tenantId}:draft:{lineUserId}:{yyyy-MM-dd}
```
具體 key：`e2e-test-tenant:draft:Ue2e000000000000000000000000test:{today}`（`today` = 台灣時區當日 yyyy-MM-dd）

**JSON 序列化規則**（來自 P3-001 §4.3）：
- 命名策略：`JsonNamingPolicy.SnakeCaseLower`（**snake_case**）
- TTL：`86400` 秒（24 小時）
- 寫入指令：Redis `SET key value EX 86400`

**DraftMenuSession JSON 欄位對應（snake_case）**：

| C# 欄位名稱 | JSON key（snake_case） | E2E 測試植入值 |
|------------|----------------------|--------------|
| `TenantId` | `tenant_id` | `"e2e-test-tenant"` |
| `LineUserId` | `line_user_id` | `"Ue2e000000000000000000000000test"` |
| `Date` | `date` | `"{today}"` |
| `Items` | `items` | 陣列（見下方品項規格） |
| `CreatedAt` | `created_at` | `"{now ISO 8601}"` |
| `UpdatedAt` | `updated_at` | `"{now ISO 8601}"` |

**DraftItem 欄位對應（snake_case）**：

| C# 欄位名稱 | JSON key（snake_case） | E2E 測試植入值（品項 1） |
|------------|----------------------|----------------------|
| `Id` | `id` | `"e2efeed000000000000000000000201"` |
| `Name` | `name` | `"E2E 發布測試青菜"` |
| `IsNew` | `is_new` | `false` |
| `BuyPrice` | `buy_price` | `100.00` |
| `SellPrice` | `sell_price` | `150.00` |
| `Quantity` | `quantity` | `5` |
| `Unit` | `unit` | `"斤"` |
| `HistoricalAvgPrice` | `historical_avg_price` | `null` |
| `Validation` | `validation` | `{ "status": "ok", "message": null }` |

> ⚠️ **關鍵對齊要求**：`validation` 子物件的欄位結構（`status`, `message`）必須與 P3-001 §7 定義的 `DraftItemDto.validation` 一致。E2E Test Agent 在實作 `seedDraftToRedis()` 時，**必須先查閱後端 `ValidationResult` C# 型別的實際欄位名稱與 JSON 命名**，確保反序列化正確。若後端 `ValidationResult` 使用不同欄位名稱（如 `is_valid`），須以後端實作為準。

### clearPublishedMenusForTenant() 函式規格

**目的**：清除指定租戶的 DB 記錄（不插入資料），供 `publish-flow.spec.ts` 的 `beforeEach` 使用。

**SQL**：`DELETE FROM published_menus WHERE tenant_id = $1`（$1 = tenantId，CASCADE 刪除 published_menu_items）

**同時清除 Redis cache**：`DEL "{tenantId}:menu:published:{today}"`（避免 write-through 快取污染）

### PostgreSQL 直接查詢策略

E2E 測試使用 **API 回應 + DB 直查雙重驗證**（不僅依賴 HTTP response）：

| 驗證點 | 驗證方式 | 說明 |
|--------|---------|------|
| 發布後 DB 寫入 | `SELECT id, date, tenant_id FROM published_menus WHERE tenant_id=$1 AND date=$2` | 確認持久化成功 |
| 發布後品項寫入 | `SELECT id, original_qty, remaining_qty FROM published_menu_items WHERE menu_id=$1` | 確認品項 + 初始庫存 |
| 庫存扣除後 DB 值 | `getItemRemainingQty(itemId)` (現有函式) | 確認 DB 正確遞減 |
| 草稿刪除 | Redis `EXISTS "{tenantId}:draft:{userId}:{today}"` → 應為 0 | 確認發布後草稿清除 |

---

## 安全設計

**觸發條件**：安全標籤勾選「涉及不可逆操作（測試資料寫入真實 DB）」

### 信任邊界分析

| 資料流 | 來源 | 目的 | 跨越邊界 | 敏感度 | 威脅 | 緩解策略 |
|--------|------|------|----------|--------|------|----------|
| Seed SQL 執行 | CI/測試環境 | PostgreSQL（veggieally DB） | 內部→資料層 | 低（全部為虛構測試資料） | Seed 污染非測試 DB | 明確隔離機制（見下） |
| Redis Draft 植入 | `seedDraftToRedis()` | Redis（測試環境） | 內部→資料層 | 低（虛構 tenant/user） | Key 命名衝突 | Key 含 `e2e-` 前綴，與生產 key 空間分離 |
| E2E 測試 API 呼叫 | Playwright Test Runner | `http://localhost:5010`（容器） | 外部→內部 | 低（mock auth，Testing env only） | X-Test-Auth bypass 外洩 | ASPNETCORE_ENVIRONMENT=Testing 嚴格隔離，Production 無此路徑 |

### 不可逆操作防護策略

本任務的「不可逆操作」是指：**測試資料寫入真實 PostgreSQL DB（非 mock）**。防護機制如下：

| 操作 | 不可逆類型 | 防護機制 | 冪等性 | 稽核要求 |
|------|-----------|----------|--------|----------|
| Seed SQL 執行（`001_e2e_test_seed.sql`） | DB 寫入 | DELETE + INSERT 確保確定性狀態；`db-init` 容器 `restart: "no"` 確保只執行一次 | ✅ 每次執行結果相同（刪除後重建） | 不需要（測試環境，無業務意義資料） |
| `resetAndSeedTestData()` | DB 寫入（per-test） | 在 `BEGIN/COMMIT` Transaction 中執行；失敗自動 `ROLLBACK` | ✅ 每次 `beforeEach` 重置為相同初始狀態 | 不需要 |
| `seedDraftToRedis()` | Redis 寫入 | Redis `SET … EX 86400`（TTL 自動過期） | ✅ 覆寫式寫入，重複執行不累積 | 不需要 |

### 測試環境與生產環境隔離邊界

**必須遵守的隔離規則**（硬性約束，不得例外）：

1. **`docker-compose.test.yml` 嚴禁用於生產部署**：檔案頭部已有聲明，DBA Agent 修改此檔案時須保留此聲明。
2. **Seed SQL 不得進入生產 migration 流程**：`db/seeds/` 與 `db/migrations/` 目錄分離；`db-init` 在生產環境不得掛載 `/seeds` volume。
3. **X-Test-Auth bypass 僅在 `ASPNETCORE_ENVIRONMENT=Testing` 生效**：後端實作已確保此限制（P3-001 §8.2）；E2E Test Agent 不得嘗試在 Development/Production 環境呼叫此路徑。
4. **測試租戶 ID 命名規範**：所有測試資料使用 `e2e-` 前綴，便於識別與清理（`DELETE … WHERE tenant_id LIKE 'e2e-%'`）；生產資料不使用此前綴。
5. **DB 帳號最小權限**：`db-init` 容器使用 `veggie` 帳號（具備 DDL 與 DML 權限），僅供 Testing 環境；生產環境建議依 `db/README.md §安全性與權限` 分離 migrator/writer/reader 帳號。

### 冪等性保證

| 元件 | 冪等機制 | 驗證方式 |
|------|---------|---------|
| `001_e2e_test_seed.sql` | DELETE（tenant_id='e2e-seed-tenant'）+ INSERT | 執行兩次後查詢結果應相同 |
| `resetAndSeedTestData()` | DELETE（tenant_id LIKE 'e2e-%'）+ INSERT | 現有實作，已在 `inventory.spec.ts` 驗證 |
| `clearPublishedMenusForTenant()` | DELETE（tenant_id=$1）| 純刪除，天然冪等 |
| `seedDraftToRedis()` | SET … EX（覆寫式） | 天然冪等 |

---

## 參考 Skill（供後端 PG 參考，本任務不修改後端）

> 本任務不涉及後端實作變更，以下 Skill 僅供 E2E Test Agent 理解後端行為時參考：
- `dotnet-cqrs-command`：`PublishMenuCommand`、`DeductInventoryCommand` 執行路徑
- `dotnet-result-pattern`：後端錯誤處理策略（影響 E2E 斷言的 error body 格式）

---

## Agent Handoff Contract

> ⚠️ 此章節為強制欄位。下游 Agent 開工前必須完整閱讀。

### 前提假設（下游 Agent 不得違反）

1. **PostgreSQL schema 已存在**：`published_menus` 與 `published_menu_items` 表及其 constraints/indexes 已由 `001_create_published_menus.sql` 建立。DBA Agent 產出的 seed SQL **不得**包含 DDL（無 CREATE TABLE）。
2. **Redis key 格式已凍結**：Draft key = `{tenantId}:draft:{lineUserId}:{yyyy-MM-dd}`；Published cache key = `{tenantId}:menu:published:{yyyy-MM-dd}`。E2E Test Agent 必須使用這兩個確切格式。
3. **JSON 序列化格式已凍結**：DraftMenuSession 在 Redis 中使用 `JsonNamingPolicy.SnakeCaseLower`（P3-001 §4.3）。E2E Test Agent 的 `seedDraftToRedis()` 必須生成符合此格式的 JSON，否則後端 `JsonSerializer.Deserialize<DraftMenuSession>` 會失敗。
4. **Mock auth 機制已凍結**：`ASPNETCORE_ENVIRONMENT=Testing` 下，後端接受 `X-Test-Auth: true` + `X-Test-TenantId` + `X-Test-LineUserId` + `X-Test-DisplayName` headers。E2E Test Agent 使用現有 `testAuthHeaders()` 函式，不修改 mock auth 機制。
5. **測試執行為序列模式（workers: 1）**：`playwright.config.ts` 已配置 `workers: 1`，所有 test 序列執行，DB 共享狀態由各自的 `beforeEach` 管理，不需額外並發保護。
6. **`db.ts` 現有函式不修改**：`resetAndSeedTestData()`、`closeDatabasePool()`、`getItemRemainingQty()`、所有常數（`SEED_MENU_ID`、`SEED_ITEM_1_ID` 等）保持不變，`inventory.spec.ts` 繼續使用現有 helpers。
7. **`db-init` 失敗為阻塞條件**：若 seed SQL 執行失敗，`db-init` 容器 exit code ≠ 0，`veggie-ally` 服務不會啟動（`condition: service_completed_successfully`）。DBA Agent 產出的 SQL **必須**在乾淨 DB 和已有資料兩種情況下均可正常執行。

### 各 Agent 啟動包

#### DBA Agent 啟動包

**任務**：建立 `db/seeds/001_e2e_test_seed.sql` 並更新 `docker-compose.test.yml`

**必讀檔案**：
- `VeggieAlly/db/migrations/001_create_published_menus.sql`（了解 schema）
- `VeggieAlly/docker-compose.test.yml`（了解現有 db-init 指令）
- 本藍圖 §Seed SQL 設計規格（固定 ID、欄位值、冪等策略）
- 本藍圖 §docker-compose.test.yml 整合方案（volume mount + 指令位置）

**產出物**：
1. 新增 `VeggieAlly/db/seeds/001_e2e_test_seed.sql`（符合本藍圖固定 ID 與欄位值規格）
2. 修改 `VeggieAlly/docker-compose.test.yml`：
   - `db-init.volumes` 新增 `./db/seeds:/seeds:ro`
   - `db-init.command` 末尾追加 seed SQL 執行步驟

**硬性約束**：
- SQL 中不含任何 DDL（無 CREATE/ALTER/DROP）
- 僅操作 `e2e-seed-tenant` 的資料（不動其他 tenant）
- 使用本藍圖指定的固定 ID（`beefcafe…`）
- 保留 `docker-compose.test.yml` 頭部安全聲明
- `published_menus.date` 使用 `'2020-01-01'`（歷史日期，不與今日衝突）

#### E2E Test Agent 啟動包

**任務**：新增 `publish-flow.spec.ts` 測試案例與 `db.ts` 輔助函式

**必讀檔案**：
- `VeggieAlly/tests/e2e/helpers/auth.ts`（現有 mock auth helpers）
- `VeggieAlly/tests/e2e/helpers/db.ts`（現有 DB helpers；**勿修改**現有函式）
- `VeggieAlly/tests/e2e/tests/inventory.spec.ts`（了解現有測試組織方式）
- `VeggieAlly/tests/e2e/playwright.config.ts`（baseURL + workers 設定）
- 本藍圖 §E2E 測試案例設計（完整 BDD → 測試對應、Redis key 格式、JSON 欄位規格）
- 本藍圖 §時序邏輯（完整真實資料流執行路徑 ① ~ ⑤）

**產出物**：
1. `VeggieAlly/tests/e2e/helpers/db.ts`：追加以下函式（不修改現有程式碼）：
   - `seedDraftToRedis(tenantId?, userId?, today?)` — 植入 DraftMenuSession JSON 至 Redis
   - `clearPublishedMenusForTenant(tenantId)` — 清除指定租戶的 DB + Redis cache
   - `getPublishedMenuByTenantAndDate(tenantId, date)` — 查詢 published_menus（回傳 row 或 null）
   - `getPublishedItemsByMenuId(menuId)` — 查詢 published_menu_items（回傳 row[]）
2. `VeggieAlly/tests/e2e/tests/publish-flow.spec.ts`：對應 SC-39-02-01（Happy Path 完整流程）+ SC-39-02-02（重複發布 409）+ SC-39-01-02（無效 JSON）

**硬性約束**：
- `ValidationResult` JSON 格式（`validation` 子物件欄位名稱）**必須先查閱後端 C# 原始碼確認**，不得假設。查閱路徑：`VeggieAlly/src/` 目錄中搜尋 `ValidationResult` class 定義。
- `seedDraftToRedis()` 中的 `date` 欄位使用台灣時區（`todayTaiwan()` 函式，現有實作）
- 不引入任何新的 npm 套件（`ioredis` 已在 `package.json`，Redis 直寫已可用）
- 所有新測試的 `beforeEach` 必須呼叫 `clearPublishedMenusForTenant()` + `seedDraftToRedis()`（不得依賴 `resetAndSeedTestData()`，因後者會插入已發布菜單，與發布流程測試衝突）

### 依賴順序

```
DBA Agent（Seed SQL + docker-compose 修改）
    │
    └─→ E2E Test Agent（可平行，但需等 docker-compose 環境可用後驗收）
```

> E2E Test Agent 可以在 DBA Agent 完成前就開始撰寫測試程式碼（測試邏輯不依賴 seed SQL 內容），但最終驗收跑測試需要 DBA Agent 的產出物已合入且 docker-compose 環境可啟動。

### 架構決策記錄

| 決策主題 | 選擇方案 | 被拒絕方案 | 拒絕理由 |
|---------|---------|-----------|---------|
| Per-test DB 重置策略（publish-flow） | `clearPublishedMenusForTenant()` 清空後，`seedDraftToRedis()` 植入 Redis | 複用 `resetAndSeedTestData()` | `resetAndSeedTestData()` 會插入已發布菜單（直接 INSERT 到 DB），與測試 POST /publish 的前提「DB 無今日記錄」衝突 |
| 草稿建立模擬方式 | 直接 Redis 寫入（test helper `seedDraftToRedis()`） | 呼叫 LINE Webhook endpoint 觸發草稿建立 | Testing 環境 `AI__Provider=ollama`，endpoint = dummy，LLM 解析不可用；LINE Webhook 需真實 LINE Platform 呼叫，E2E 環境無法模擬 |
| Seed SQL 冪等策略 | DELETE + INSERT | ON CONFLICT DO NOTHING | ON CONFLICT DO NOTHING 在欄位值變更時不更新，可能保留舊的不一致資料；DELETE + INSERT 確保每次都是已知確定狀態 |
| 靜態 seed 租戶命名 | `e2e-seed-tenant` | `seed-tenant`（不含 e2e- 前綴） | 測試清理條件 `LIKE 'e2e-%'` 本就應涵蓋所有測試資料；使用 `e2e-` 前綴明確標示為測試租戶，避免命名混淆 |
| DB 狀態驗證方式 | HTTP response + 直接 PostgreSQL 查詢雙重驗證 | 僅依賴 HTTP response | Issue #39 明確要求「驗證 PostgreSQL 實際資料狀態（不 mock Repository）」；純 HTTP 驗證無法確認 DB 寫入是否真正持久化 |
| 測試執行序列 | 維持 `workers: 1` 序列執行 | 並行執行 | DB 共享狀態（published_menus 每日每租戶唯一）在並行執行時會因測試互相干擾；序列執行已由 `playwright.config.ts` 設定，不變更 |

### ADR 引用

- ADR-001：漸進式工作流採漸進式導入策略（`docs/specs/adr/ADR-001-multi-agent-workflow-progressive-adoption.md`）— 本任務為 L2 任務，Orchestrator 直接派工
- ADR-002：QA/QC 藍圖審查閘門（`docs/specs/adr/ADR-002-qa-qc-blueprint-review-gate.md`）— **本藍圖 commit 後，必須由 QA/QC 完成 Gate 1 審查，通過後才能啟動 DBA + E2E Test Agent 開工**
- **無新建 ADR**：本任務所有決策在已知架構邊界內，不涉及新的重要架構方向改變

### 給下一個 Agent 的提醒

**DBA 注意**：
- `db/seeds/` 目錄不存在，需先建立目錄
- `docker-compose.test.yml` 的 `db-init.command` 是 multiline shell script，新增步驟須使用 `&&` 串接
- Seed SQL 中的 `published_menus.date` 欄位型別為 `DATE`，插入時需使用 `'2020-01-01'::date` 或直接 `'2020-01-01'`（PostgreSQL 隱式轉換）
- 注意 `published_menu_items` 有 `chk_remaining_qty CHECK (remaining_qty >= 0)` 與 `chk_prices CHECK (buy_price >= 0 AND sell_price >= 0)` 約束，seed 資料須符合

**E2E Test Agent 注意**：
- `ValidationResult` 的 JSON 欄位結構是最關鍵的對齊點：若後端 `DraftMenuSession.Items[].Validation` 在 Redis 中序列化的格式與 `seedDraftToRedis()` 產生的 JSON 不符，`POST /api/menu/publish` 將失敗（後端反序列化錯誤）。**必須先從後端原始碼確認，再實作此函式**
- `publish-flow.spec.ts` 中的 Happy Path 測試（SC-39-02-01）結束後，DB 中會留有今日的 published_menus 記錄；若後續 `inventory.spec.ts` 在同一次 CI run 中執行，`resetAndSeedTestData()` 會正確清除（`DELETE … WHERE tenant_id LIKE 'e2e-%'`），不影響
- `POST /api/menu/publish` response body 格式參考 P3-002 §7.3 `PublishedMenuDto`；201 回應的 item id 是後端在發布時生成的新 GUID（不等於草稿的 item id），斷言時應使用 `toBeGreaterThanOrEqual(1)` 而非固定 ID

---

*藍圖版本：v1.0 | 最後更新：2026-05-14 | 產出者：SA/SD Agent*
