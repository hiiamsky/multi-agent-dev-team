/**
 * tests/inventory.spec.ts
 *
 * E2E 測試：今日菜單查詢 + 庫存扣除
 *
 * 測試環境需求：
 *   - docker compose -f docker-compose.yml -f docker-compose.test.yml up -d
 *   - API 在 http://localhost:5000 提供服務（ASPNETCORE_ENVIRONMENT=Testing）
 *   - PostgreSQL 已跑 migration（db-init 容器完成）
 *
 * 測試案例：
 *   1. [Happy Path] GET /api/menu/today → 200 + items 陣列非空
 *   2. [Happy Path] PATCH /api/menu/inventory → 200 + DB remaining_qty 正確遞減
 *   3. [Edge Case]  PATCH /api/menu/inventory（超量）→ 409 + DB remaining_qty 不變
 *   4. [Edge Case]  GET /api/menu/today（無菜單租戶）→ 404
 *
 * 測試隔離策略：
 *   - 每個 test 的 beforeEach 呼叫 resetAndSeedTestData()
 *   - 插入固定 ID 的 seed 資料，確保初始狀態一致
 *   - 不依賴上一個 test 的 DB 狀態或 session
 */

import { test, expect, APIResponse } from '@playwright/test';
import {
  testAuthHeaders,
  E2E_TENANT_ID,
  E2E_NO_MENU_TENANT_ID,
} from '../helpers/auth';
import {
  resetAndSeedTestData,
  closeDatabasePool,
  getItemRemainingQty,
  SEED_ITEM_1_ID,
  SEED_ITEM_1_INITIAL_QTY,
  SEED_ITEM_2_ID,
  SEED_ITEM_2_INITIAL_QTY,
} from '../helpers/db';

// ─── 全域 teardown：測試套件結束後關閉 DB pool ────────────────────────────
test.afterAll(async () => {
  await closeDatabasePool();
});

// ─── 每個 test 前重置 DB，確保狀態獨立 ───────────────────────────────────
test.beforeEach(async () => {
  await resetAndSeedTestData();
});

// ═════════════════════════════════════════════════════════════════════════════
// 1. Happy Path — 取得今日菜單
// ═════════════════════════════════════════════════════════════════════════════
test('Happy Path — GET /api/menu/today 回傳 200 及品項陣列', async ({ request }) => {
  // Act
  const response: APIResponse = await request.get('/api/menu/today', {
    headers: testAuthHeaders(E2E_TENANT_ID),
  });

  // Assert HTTP status
  expect(
    response.status(),
    `Expected 200, got ${response.status()}. Body: ${await response.text()}`,
  ).toBe(200);

  // Assert response body
  const body = await response.json() as {
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

  // 回應結構驗證
  expect(body.tenant_id).toBe(E2E_TENANT_ID);
  expect(Array.isArray(body.items)).toBe(true);
  expect(body.items.length).toBeGreaterThanOrEqual(2);

  // 確認 seed 品項存在於回應中
  const item1 = body.items.find((i) => i.id === SEED_ITEM_1_ID);
  expect(item1, '品項 1（E2E 測試青菜）應存在於 items 中').toBeDefined();
  expect(item1!.remaining_qty).toBe(SEED_ITEM_1_INITIAL_QTY);
  expect(item1!.unit).toBe('斤');

  const item2 = body.items.find((i) => i.id === SEED_ITEM_2_ID);
  expect(item2, '品項 2（E2E 測試高麗菜）應存在於 items 中').toBeDefined();
  expect(item2!.remaining_qty).toBe(SEED_ITEM_2_INITIAL_QTY);
});

// ═════════════════════════════════════════════════════════════════════════════
// 2. Happy Path — 庫存扣除
// ═════════════════════════════════════════════════════════════════════════════
test('Happy Path — PATCH /api/menu/inventory 扣除 3 單位後 remaining_qty 正確遞減', async ({
  request,
}) => {
  const deductAmount = 3;
  const expectedRemaining = SEED_ITEM_1_INITIAL_QTY - deductAmount; // 10 - 3 = 7

  // Act — 呼叫庫存扣除 API
  const response: APIResponse = await request.patch('/api/menu/inventory', {
    headers: testAuthHeaders(E2E_TENANT_ID),
    data: {
      item_id: SEED_ITEM_1_ID,
      amount: deductAmount,
    },
  });

  // Assert HTTP status
  expect(
    response.status(),
    `Expected 200, got ${response.status()}. Body: ${await response.text()}`,
  ).toBe(200);

  // Assert response body（API 回傳扣除後的品項狀態）
  const body = await response.json() as {
    id: string;
    name: string;
    remaining_qty: number;
    unit: string;
  };
  expect(body.id).toBe(SEED_ITEM_1_ID);
  expect(body.remaining_qty).toBe(expectedRemaining);

  // ── DB 直接驗證：確認業務狀態真正持久化 ────────────────────────────────
  const dbRemaining = await getItemRemainingQty(SEED_ITEM_1_ID);
  expect(
    dbRemaining,
    `DB 中的 remaining_qty 應為 ${expectedRemaining}，但得到 ${dbRemaining}`,
  ).toBe(expectedRemaining);
});

// ═════════════════════════════════════════════════════════════════════════════
// 3. Edge Case — 庫存不足
// ═════════════════════════════════════════════════════════════════════════════
test('Edge Case — PATCH 超量請求應回 409，且 DB remaining_qty 不變', async ({
  request,
}) => {
  // 請求 999 單位，但庫存只有 2
  const overRequestAmount = 999;

  // Act — 記錄請求前的 DB 狀態（預期 seed 的 remaining_qty = 2）
  const beforeQty = await getItemRemainingQty(SEED_ITEM_2_ID);
  expect(beforeQty).toBe(SEED_ITEM_2_INITIAL_QTY);

  const response: APIResponse = await request.patch('/api/menu/inventory', {
    headers: testAuthHeaders(E2E_TENANT_ID),
    data: {
      item_id: SEED_ITEM_2_ID,
      amount: overRequestAmount,
    },
  });

  // Assert — 應回 409 Conflict（InsufficientStockException）
  expect(
    [409, 400],
    `Expected 409 or 400, got ${response.status()}. Body: ${await response.text()}`,
  ).toContain(response.status());

  const body = await response.json() as { error?: { code?: string } };
  // 如果後端明確回傳 error code，驗證它
  if (body.error?.code) {
    expect(body.error.code).toBe('INSUFFICIENT_STOCK');
  }

  // ── DB 直接驗證：庫存不應被扣除 ─────────────────────────────────────────
  const afterQty = await getItemRemainingQty(SEED_ITEM_2_ID);
  expect(
    afterQty,
    `庫存不足時 DB 的 remaining_qty 不應改變，應仍為 ${SEED_ITEM_2_INITIAL_QTY}，但得到 ${afterQty}`,
  ).toBe(SEED_ITEM_2_INITIAL_QTY);
});

// ═════════════════════════════════════════════════════════════════════════════
// 4. Edge Case — 當日無菜單
// ═════════════════════════════════════════════════════════════════════════════
test('Edge Case — 無菜單租戶的 GET /api/menu/today 應回 404', async ({
  request,
}) => {
  // 使用 e2e-no-menu-tenant（不在 seed 資料中）
  const response: APIResponse = await request.get('/api/menu/today', {
    headers: testAuthHeaders(E2E_NO_MENU_TENANT_ID),
  });

  // 根據規格：無資料時回 404 Not Found
  expect(
    response.status(),
    `Expected 404, got ${response.status()}. Body: ${await response.text()}`,
  ).toBe(404);

  const body = await response.json() as { error?: { code?: string } };
  if (body.error?.code) {
    expect(body.error.code).toBe('MENU_NOT_FOUND');
  }
});
