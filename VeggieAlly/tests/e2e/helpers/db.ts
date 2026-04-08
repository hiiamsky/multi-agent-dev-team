/**
 * helpers/db.ts
 *
 * PostgreSQL 直連輔助（使用 node-postgres / pg）
 * Redis 快取清除輔助（使用 ioredis）
 *
 * 目的：
 *   - 在每個 test 的 beforeEach 中重置 e2e-test-tenant 的 DB 狀態
 *   - 清除 Redis 快取（避免 write-through cache 造成跨測試污染）
 *   - 插入固定的 seed 資料，確保測試初始條件一致
 *   - 在測試斷言中直接查詢 DB，確認業務狀態（不只依賴 HTTP response）
 *
 * 連線設定（以環境變數控制，不 hardcode 密碼）：
 *   DB_HOST     預設 localhost
 *   DB_PORT     預設 5432
 *   DB_NAME     預設 veggieally
 *   DB_USER     預設 veggie
 *   DB_PASSWORD 預設 veggie_dev（docker-compose 開發/測試用值）
 *   REDIS_HOST  預設 localhost
 *   REDIS_PORT  預設 6379
 *
 * 安全說明：
 *   測試資料皆為假資料，不包含真實使用者 PII 或生產環境憑證。
 */

import { Pool, PoolClient } from 'pg';
import Redis from 'ioredis';
import { E2E_TENANT_ID, E2E_LINE_USER_ID } from './auth';

// ─── 固定 Seed 資料 ID（GUID "N" 格式，32 hex chars）──────────────────────
export const SEED_MENU_ID   = 'deadbeef000000000000000000000001';
export const SEED_ITEM_1_ID = 'deadbeef000000000000000000000101'; // 充足庫存（10）
export const SEED_ITEM_2_ID = 'deadbeef000000000000000000000102'; // 少量庫存（2）

export const SEED_ITEM_1_INITIAL_QTY = 10;
export const SEED_ITEM_2_INITIAL_QTY = 2;

// ─── PostgreSQL Pool 單例 ──────────────────────────────────────────────────
let _pool: Pool | null = null;

function getPool(): Pool {
  if (!_pool) {
    _pool = new Pool({
      host:     process.env['DB_HOST']     ?? 'localhost',
      port:     Number(process.env['DB_PORT'])  || 5432,
      database: process.env['DB_NAME']     ?? 'veggieally',
      user:     process.env['DB_USER']     ?? 'veggie',
      password: process.env['DB_PASSWORD'] ?? 'veggie_dev',
      connectionTimeoutMillis: 5000,
      idleTimeoutMillis: 10000,
      max: 5,
    });
  }
  return _pool;
}

// ─── Redis 單例 ────────────────────────────────────────────────────────────
let _redis: Redis | null = null;

function getRedis(): Redis {
  if (!_redis) {
    _redis = new Redis({
      host: process.env['REDIS_HOST'] ?? 'localhost',
      port: Number(process.env['REDIS_PORT']) || 6379,
      lazyConnect: true,
      enableOfflineQueue: false,
      connectTimeout: 3000,
      maxRetriesPerRequest: 1,
    });
  }
  return _redis;
}

/**
 * 關閉 Pool 和 Redis 連線（測試套件結束後呼叫）
 */
export async function closeDatabasePool(): Promise<void> {
  if (_pool) {
    await _pool.end();
    _pool = null;
  }
  if (_redis) {
    _redis.disconnect();
    _redis = null;
  }
}

/**
 * 取得今日日期（Taiwan UTC+8），格式 yyyy-MM-dd
 * 與後端 GetTodayMenuHandler 使用相同的時區基準
 */
export function todayTaiwan(): string {
  const now = new Date();
  // Taiwan = UTC+8
  const taiwanMs = now.getTime() + 8 * 60 * 60 * 1000;
  return new Date(taiwanMs).toISOString().slice(0, 10);
}

/**
 * 清除指定租戶在 Redis 中的已發布菜單快取。
 * 若 Redis 連線失敗，靜默略過（不影響測試流程）。
 */
async function flushTenantMenuCache(tenantId: string, date: string): Promise<void> {
  const redis = getRedis();
  try {
    await redis.connect().catch(() => {/* already connected */});
    const key = `${tenantId}:menu:published:${date}`;
    await redis.del(key);
  } catch {
    // Redis 清除失敗時靜默略過，不應阻斷測試
  }
}

/**
 * 重置 E2E 測試用的 DB 狀態，並植入固定 seed 資料。
 * 同時清除相關的 Redis 快取，確保快取不污染測試結果。
 *
 * 在 beforeEach 呼叫，確保每個 test 都從相同的乾淨狀態開始。
 */
export async function resetAndSeedTestData(): Promise<void> {
  const today = todayTaiwan();

  // 1. 先清除 Redis 快取（避免 write-through 快取污染）
  await flushTenantMenuCache(E2E_TENANT_ID, today);

  // 2. 重置 DB
  const client: PoolClient = await getPool().connect();
  try {
    await client.query('BEGIN');

    // 清除所有 e2e 租戶資料（CASCADE 自動刪除 published_menu_items）
    await client.query(
      `DELETE FROM published_menus WHERE tenant_id LIKE 'e2e-%'`,
    );

    // 插入今日發布菜單（Taiwan 日期）
    await client.query(
      `INSERT INTO published_menus
         (id, tenant_id, published_by, date, published_at)
       VALUES ($1, $2, $3, $4::date, NOW())`,
      [SEED_MENU_ID, E2E_TENANT_ID, E2E_LINE_USER_ID, today],
    );

    // 品項 1：充足庫存（original=10, remaining=10）
    await client.query(
      `INSERT INTO published_menu_items
         (id, menu_id, tenant_id, name, is_new, buy_price, sell_price,
          original_qty, remaining_qty, unit)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)`,
      [
        SEED_ITEM_1_ID,
        SEED_MENU_ID,
        E2E_TENANT_ID,
        'E2E 測試青菜',
        false,
        100.00,
        150.00,
        SEED_ITEM_1_INITIAL_QTY,
        SEED_ITEM_1_INITIAL_QTY,
        '斤',
      ],
    );

    // 品項 2：少量庫存（original=2, remaining=2）用於庫存不足測試
    await client.query(
      `INSERT INTO published_menu_items
         (id, menu_id, tenant_id, name, is_new, buy_price, sell_price,
          original_qty, remaining_qty, unit)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)`,
      [
        SEED_ITEM_2_ID,
        SEED_MENU_ID,
        E2E_TENANT_ID,
        'E2E 測試高麗菜',
        true,
        50.00,
        80.00,
        SEED_ITEM_2_INITIAL_QTY,
        SEED_ITEM_2_INITIAL_QTY,
        '顆',
      ],
    );

    await client.query('COMMIT');
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    client.release();
  }
}

/**
 * 查詢指定品項的剩餘庫存（直接從 DB 讀取，不透過 HTTP API）
 *
 * @param itemId  published_menu_items.id
 * @returns remaining_qty，若找不到回傳 null
 */
export async function getItemRemainingQty(itemId: string): Promise<number | null> {
  const result = await getPool().query<{ remaining_qty: number }>(
    `SELECT remaining_qty FROM published_menu_items WHERE id = $1`,
    [itemId],
  );
  if (result.rows.length === 0) return null;
  return Number(result.rows[0].remaining_qty);
}

