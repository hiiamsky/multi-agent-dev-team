-- ============================================================
-- E2E Integration Test Seed Data
-- Tenant  : e2e-seed-tenant
-- Strategy: DELETE + INSERT（冪等，可重複執行）
--
-- 注意：此 seed 僅操作 e2e-seed-tenant 資料
--       禁止 DDL（無 CREATE/ALTER/DROP）
--       id 欄位使用 GUID "N" 格式（VARCHAR(32)，無連字符）
-- ============================================================

-- ─── Step 1: 清除舊 seed 資料（子表先刪，主表後刪）────────────────────────

DELETE FROM published_menu_items
WHERE menu_id IN (
    SELECT id FROM published_menus WHERE tenant_id = 'e2e-seed-tenant'
);

DELETE FROM published_menus
WHERE tenant_id = 'e2e-seed-tenant';

-- ─── Step 2: 插入靜態 seed 菜單主表 ──────────────────────────────────────
-- id: beefcafe-0000-0000-0000-000000000001 → GUID "N" format
-- date: 歷史日期 2020-01-01，不與今日菜單衝突

INSERT INTO published_menus (id, tenant_id, published_by, date, published_at)
VALUES (
    'beefcafe000000000000000000000001',  -- GUID N-format
    'e2e-seed-tenant',
    'e2e-seed-bot',
    '2020-01-01',
    '2020-01-01T00:00:00+00:00'
);

-- ─── Step 3: 插入靜態 seed 品項 ──────────────────────────────────────────
-- Item 1: 測試蔬菜 A  (id: beefcafe-0000-0000-0000-000000000101)
-- Item 2: 測試蔬菜 B  (id: beefcafe-0000-0000-0000-000000000102)
--
-- 約束確認：
--   chk_remaining_qty  : remaining_qty >= 0  ✓ (100, 50)
--   chk_original_qty   : original_qty >= 0   ✓ (100, 50)
--   chk_prices         : buy_price >= 0
--                        AND sell_price >= 0  ✓

INSERT INTO published_menu_items
    (id, menu_id, tenant_id, name, is_new, buy_price, sell_price,
     original_qty, remaining_qty, unit)
VALUES
    (
        'beefcafe000000000000000000000101',  -- GUID N-format
        'beefcafe000000000000000000000001',
        'e2e-seed-tenant',
        '測試蔬菜 A',
        FALSE,
        10.00,
        15.00,
        100,   -- original_qty
        100,   -- remaining_qty
        '份'
    ),
    (
        'beefcafe000000000000000000000102',  -- GUID N-format
        'beefcafe000000000000000000000001',
        'e2e-seed-tenant',
        '測試蔬菜 B',
        FALSE,
        20.00,
        30.00,
        50,    -- original_qty
        50,    -- remaining_qty
        '份'
    );
