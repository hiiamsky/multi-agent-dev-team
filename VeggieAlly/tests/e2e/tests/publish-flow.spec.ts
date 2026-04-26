/**
 * tests/publish-flow.spec.ts
 *
 * E2E 測試：菜單發布完整真實資料流
 *
 * 對應 BDD Scenarios（P4-001 藍圖）：
 *   SC-39-02-01  發布成功，PostgreSQL 寫入正確（Happy Path 完整流程：草稿→發布→查詢→庫存扣除 DB 驗證）
 *   SC-39-02-02  重複發布同日菜單，回 409（DB 記錄不重複插入）
 *   SC-39-01-02  草稿 JSON 格式不符，後端反序列化失敗，POST /publish 回非 2xx
 *
 * 測試環境需求：
 *   - docker compose -f docker-compose.yml -f docker-compose.test.yml up -d
 *   - API 在 http://localhost:5010 提供服務（ASPNETCORE_ENVIRONMENT=Testing）
 *   - PostgreSQL 與 Redis 均已可連線
 *
 * 測試隔離策略：
 *   每個 test 的 beforeEach 呼叫：
 *     1. clearPublishedMenusForTenant(TEST_TENANT_ID) — 清除 DB + Redis 已發布快取
 *     2. seedDraftToRedis()                           — 植入有效 DraftMenuSession 草稿
 *   不依賴 resetAndSeedTestData()（後者會插入已發布菜單，與發布流程測試衝突）
 *
 * 安全設計說明（security-baseline §認證生命週期）：
 *   - 測試全程使用 X-Test-Auth mock header（ASPNETCORE_ENVIRONMENT=Testing 才生效）
 *   - 所有測試資料為假資料，租戶 ID 含 "e2e-" 前綴，不包含任何真實 PII
 *   - 不在測試腳本中寫死任何真實使用者憑證或生產環境密碼
 *
 * 後端實作差異備注（已知，待批判回饋）：
 *   - POST /api/menu/publish 201 response 回傳 items_count（整數），
 *     P4-001 藍圖 SC-39-02-01 規格描述為 items 陣列，實際實作不同。
 *     本測試依實際後端行為斷言。詳見文末 E2E Critique。
 *   - SC-39-02-02 409 error.code 實際為 "ALREADY_PUBLISHED"，
 *     藍圖描述為 "MENU_ALREADY_PUBLISHED"。本測試依實際後端行為斷言。
 */

import { test, expect } from '@playwright/test';
import Redis from 'ioredis';
import {
  testAuthHeaders,
  E2E_TENANT_ID,
  E2E_LINE_USER_ID,
} from '../helpers/auth';
import {
  clearPublishedMenusForTenant,
  seedDraftToRedis,
  getPublishedMenuByTenantAndDate,
  getPublishedItemsByMenuId,
  countPublishedMenusForTenantAndDate,
  closeDatabasePool,
  todayTaiwan,
} from '../helpers/db';

/** E2E 發布流程測試使用的租戶 ID（與 auth.ts 的 E2E_TENANT_ID 一致） */
const TEST_TENANT_ID = E2E_TENANT_ID;

// ─── 全域 teardown：測試套件結束後關閉 DB pool ────────────────────────────
test.afterAll(async () => {
  await closeDatabasePool();
});

// ─── 每個 test 前清除已發布菜單並植入新鮮草稿，確保發布前提成立 ─────────
test.beforeEach(async () => {
  await clearPublishedMenusForTenant(TEST_TENANT_ID);
  await seedDraftToRedis(TEST_TENANT_ID, E2E_LINE_USER_ID, todayTaiwan());
});

// ═════════════════════════════════════════════════════════════════════════════
// SC-39-02-01：發布成功，PostgreSQL 寫入正確（Happy Path 完整流程）
//
// 時序：① POST /publish → ② DB 直查 → ③ GET /today → ④ PATCH /inventory → ⑤ DB 直查
// ═════════════════════════════════════════════════════════════════════════════
test('[SC-39-02-01] 發布成功，PostgreSQL 寫入正確（Happy Path）', async ({ request }) => {
  const today = todayTaiwan();

  // ── ① When: POST /api/menu/publish ────────────────────────────────────────
  const publishResponse = await request.post('/api/menu/publish', {
    headers: testAuthHeaders(TEST_TENANT_ID, E2E_LINE_USER_ID),
  });

  // Then: HTTP 201 Created
  expect(
    publishResponse.status(),
    `Expected 201, got ${publishResponse.status()}. Body: ${await publishResponse.text()}`,
  ).toBe(201);

  // Then: Response body 結構驗證
  // 注意：後端 MenuController 回傳 items_count（整數），非 items 陣列。
  // 詳見文末 E2E Critique — 此為已知規格與實作差異，待 backend-pg 確認。
  const publishBody = await publishResponse.json() as {
    id: string;
    tenant_id: string;
    date: string;
    published_at: string;
    items_count: number;
  };

  expect(
    publishBody.id,
    'response.id 應為 32 hex chars 的 GUID "N" 格式',
  ).toMatch(/^[0-9a-f]{32}$/i);

  expect(publishBody.tenant_id).toBe(TEST_TENANT_ID);
  expect(publishBody.date).toBe(today);
  expect(
    typeof publishBody.published_at,
    'response.published_at 應為 ISO 8601 字串',
  ).toBe('string');
  expect(
    publishBody.items_count,
    'response.items_count 應 ≥ 1',
  ).toBeGreaterThanOrEqual(1);

  // ── ② DB 直接驗證：published_menus ────────────────────────────────────────
  const menuRow = await getPublishedMenuByTenantAndDate(TEST_TENANT_ID, today);
  expect(menuRow, 'published_menus 應有今日該租戶的發布記錄').not.toBeNull();
  expect(menuRow!.tenant_id).toBe(TEST_TENANT_ID);

  // DB 驗證：published_menu_items
  const itemRows = await getPublishedItemsByMenuId(menuRow!.id);
  expect(
    itemRows.length,
    'published_menu_items 應有對應品項（≥ 1 筆）',
  ).toBeGreaterThanOrEqual(1);

  // 每個品項：remaining_qty 應等於 original_qty（發布時庫存尚未扣除）
  for (const item of itemRows) {
    expect(
      Number(item.remaining_qty),
      `品項「${item.name}」的 remaining_qty 應等於 original_qty（發布時未扣除庫存）`,
    ).toBe(Number(item.original_qty));
  }

  // ── 草稿刪除驗證：發布後 Redis 草稿 key 應被清除 ──────────────────────────
  const verifyRedis = new Redis({
    host:               process.env['REDIS_HOST'] ?? 'localhost',
    port:               Number(process.env['REDIS_PORT']) || 6379,
    connectTimeout:     3000,
    maxRetriesPerRequest: 1,
    lazyConnect:        true,
  });
  try {
    await verifyRedis.connect();
    const draftKey    = `${TEST_TENANT_ID}:draft:${E2E_LINE_USER_ID}:${today}`;
    const draftExists = await verifyRedis.exists(draftKey);
    expect(
      draftExists,
      `發布後草稿 key "${draftKey}" 應已被後端刪除（PublishMenuHandler step 7）`,
    ).toBe(0);
  } finally {
    verifyRedis.disconnect();
  }

  // ── ③ GET /api/menu/today：驗證 write-through 快取讀取正確 ────────────────
  const getResponse = await request.get('/api/menu/today', {
    headers: testAuthHeaders(TEST_TENANT_ID),
  });
  expect(
    getResponse.status(),
    `Expected 200 from GET /api/menu/today, got ${getResponse.status()}. Body: ${await getResponse.text()}`,
  ).toBe(200);

  const getBody = await getResponse.json() as {
    id: string;
    tenant_id: string;
    date: string;
    items: Array<{
      id: string;
      name: string;
      sell_price: number;
      original_qty: number;
      remaining_qty: number;
      unit: string;
    }>;
  };
  expect(getBody.tenant_id).toBe(TEST_TENANT_ID);
  expect(getBody.date).toBe(today);
  expect(Array.isArray(getBody.items), 'GET /today 回應中 items 應為陣列').toBe(true);
  expect(
    getBody.items.length,
    'GET /today items 應 ≥ 1',
  ).toBeGreaterThanOrEqual(1);

  // ── ④ PATCH /api/menu/inventory：庫存扣除 1 單位 ─────────────────────────
  const targetItem    = getBody.items[0]; // 取第一個品項進行庫存扣除
  const expectedAfter = targetItem.remaining_qty - 1;

  const patchResponse = await request.patch('/api/menu/inventory', {
    headers: testAuthHeaders(TEST_TENANT_ID),
    data:    { item_id: targetItem.id, amount: 1 },
  });
  expect(
    patchResponse.status(),
    `Expected 200 from PATCH /api/menu/inventory, got ${patchResponse.status()}. Body: ${await patchResponse.text()}`,
  ).toBe(200);

  const patchBody = await patchResponse.json() as {
    id: string;
    remaining_qty: number;
  };
  expect(patchBody.id).toBe(targetItem.id);
  expect(
    patchBody.remaining_qty,
    `PATCH response.remaining_qty 應為 ${expectedAfter}（扣除 1 後）`,
  ).toBe(expectedAfter);

  // ── ⑤ DB 最終驗證：確認庫存扣除已持久化 ─────────────────────────────────
  const finalItems      = await getPublishedItemsByMenuId(menuRow!.id);
  const finalTargetItem = finalItems.find((i) => i.id === targetItem.id);
  expect(finalTargetItem, `DB 中應找到品項 ${targetItem.id}`).toBeDefined();
  expect(
    Number(finalTargetItem!.remaining_qty),
    `DB remaining_qty 應已從 ${targetItem.remaining_qty} 扣除 1，變為 ${expectedAfter}`,
  ).toBe(expectedAfter);
});

// ═════════════════════════════════════════════════════════════════════════════
// SC-39-02-02：重複發布同日菜單，回 409（DB 記錄不重複插入）
// ═════════════════════════════════════════════════════════════════════════════
test('[SC-39-02-02] 重複發布同日菜單，回 409 且 DB 記錄不重複插入', async ({ request }) => {
  const today = todayTaiwan();
  const headers = testAuthHeaders(TEST_TENANT_ID, E2E_LINE_USER_ID);

  // Given（由 beforeEach 保證）：
  //   published_menus 中不存在今日 e2e-test-tenant 記錄
  //   Redis 有有效 DraftMenuSession 草稿

  // ── 第一次發布（應成功） ───────────────────────────────────────────────────
  const firstResponse = await request.post('/api/menu/publish', { headers });
  expect(
    firstResponse.status(),
    `第一次發布應回 201，got ${firstResponse.status()}. Body: ${await firstResponse.text()}`,
  ).toBe(201);

  // ── When: 第二次發布（相同租戶同一日） ────────────────────────────────────
  const secondResponse = await request.post('/api/menu/publish', { headers });

  // Then: HTTP 409 Conflict
  expect(
    secondResponse.status(),
    `重複發布應回 409，got ${secondResponse.status()}. Body: ${await secondResponse.text()}`,
  ).toBe(409);

  // Then: response body 含 error.code（實際後端值為 "ALREADY_PUBLISHED"）
  // 注意：P4-001 藍圖描述為 "MENU_ALREADY_PUBLISHED"，實際後端回傳 "ALREADY_PUBLISHED"。
  // 本測試依實際後端行為斷言。詳見文末 E2E Critique。
  const errorBody = await secondResponse.json() as { error?: { code?: string } };
  expect(errorBody.error?.code, '重複發布 409 回應應包含 error.code').toBeDefined();
  expect(errorBody.error?.code).toBe('ALREADY_PUBLISHED');

  // ── DB 驗證：published_menus 中仍只有一筆今日記錄 ──────────────────────────
  const menuRow = await getPublishedMenuByTenantAndDate(TEST_TENANT_ID, today);
  expect(menuRow, 'published_menus 應存在今日記錄（第一次發布寫入）').not.toBeNull();

  // 直接 COUNT published_menus，確認不重複插入（斷言恰好 1 筆）
  const menuCount = await countPublishedMenusForTenantAndDate(TEST_TENANT_ID, today);
  expect(
    menuCount,
    '重複發布後 published_menus 仍應只有一筆今日記錄',
  ).toBe(1);
});

// ═════════════════════════════════════════════════════════════════════════════
// SC-39-01-02：草稿 JSON 格式不符，後端反序列化失敗，POST /publish 回非 2xx
//
// 路徑分析：
//   非法 JSON → RedisDraftSessionStore.GetAsync 捕捉 JsonException
//             → 刪除損壞 key + 回傳 null
//             → PublishMenuHandler 拋出 MenuNotPublishedException
//             → MenuController 回傳 HTTP 404 {"error":{"code":"NO_DRAFT"}}
// ═════════════════════════════════════════════════════════════════════════════
test('[SC-39-01-02] 草稿 JSON 格式不符，後端反序列化失敗，POST /publish 回 404', async ({ request }) => {
  const today = todayTaiwan();

  // Given：beforeEach 已植入有效草稿，現在將其覆寫為無效 JSON（空物件 {}）
  // 使用獨立 Redis 連線，避免干擾 db.ts 的單例
  const invalidRedis = new Redis({
    host:               process.env['REDIS_HOST'] ?? 'localhost',
    port:               Number(process.env['REDIS_PORT']) || 6379,
    connectTimeout:     3000,
    maxRetriesPerRequest: 1,
    lazyConnect:        true,
  });
  try {
    await invalidRedis.connect();
    const draftKey = `${TEST_TENANT_ID}:draft:${E2E_LINE_USER_ID}:${today}`;
    // 覆寫為無法反序列化為 DraftMenuSession 的 JSON（空物件缺少所有 required 欄位）
    await invalidRedis.set(draftKey, '{}', 'EX', 86400);
  } finally {
    invalidRedis.disconnect();
  }

  // When: POST /api/menu/publish（使用 mock auth headers）
  const response = await request.post('/api/menu/publish', {
    headers: testAuthHeaders(TEST_TENANT_ID, E2E_LINE_USER_ID),
  });

  // Then: 非 2xx 回應
  // 依後端路徑分析：JsonException → null draft → MenuNotPublishedException → HTTP 404
  expect(
    response.status(),
    `Expected 404 (draft missing after JSON parse failure), got ${response.status()}. Body: ${await response.text()}`,
  ).toBe(404);

  // Then: DB 不應有今日該租戶的新發布記錄
  const menuRow = await getPublishedMenuByTenantAndDate(TEST_TENANT_ID, today);
  expect(
    menuRow,
    '草稿 JSON 無效時，DB published_menus 不應有今日記錄',
  ).toBeNull();
});

/*
 * ═══════════════════════════════════════════════════════════════════════════
 * E2E Critique（已知差異，供 backend-pg 與 SA/SD 確認）
 * ═══════════════════════════════════════════════════════════════════════════
 *
 * ## Critique 1：POST /api/menu/publish 201 response 格式差異
 *   - P4-001 藍圖 SC-39-02-01 規格：response body 應包含 items 陣列
 *   - 實際後端 MenuController（PublishMenu action）回傳：
 *       { id, tenant_id, date, published_at, items_count }
 *     → items_count 為整數，非 items 陣列
 *   - 影響：若 API client 依賴 items 陣列，需要前端另行呼叫 GET /api/menu/today 取得
 *   - 退回對象：backend-pg（確認是否為設計決策），SA/SD（更新 API Contract）
 *   - 阻擋狀態：⚠️ 待確認（目前測試依實際行為斷言，不阻擋合併）
 *
 * ## Critique 2：SC-39-02-02 409 error.code 差異
 *   - P4-001 藍圖描述：error.code = "MENU_ALREADY_PUBLISHED"
 *   - 實際後端 MenuController：error.code = "ALREADY_PUBLISHED"
 *   - 退回對象：SA/SD（更新藍圖 error code 規格）或 backend-pg（統一命名）
 *   - 阻擋狀態：⚠️ 待確認（目前測試依實際行為斷言，不阻擋合併）
 */
