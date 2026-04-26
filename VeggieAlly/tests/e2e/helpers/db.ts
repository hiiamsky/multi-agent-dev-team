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

// ─────────────────────────────────────────────────────────────────────────────
// 以下函式為 P4-001 新增（不修改上方任何現有程式碼）
// ─────────────────────────────────────────────────────────────────────────────

/**
 * published_menus 表的查詢結果型別
 */
export interface PublishedMenuRow {
  id: string;
  tenant_id: string;
  published_by: string;
  /** node-postgres 將 DATE 欄位回傳為字串，格式 "yyyy-MM-dd" */
  date: string;
  published_at: Date;
}

/**
 * published_menu_items 表的查詢結果型別
 * 注意：node-postgres 將 NUMERIC 欄位預設回傳為字串，INTEGER 為 number
 */
export interface PublishedMenuItemRow {
  id: string;
  menu_id: string;
  tenant_id: string;
  name: string;
  is_new: boolean;
  buy_price: string;   // NUMERIC → string in node-postgres
  sell_price: string;  // NUMERIC → string in node-postgres
  original_qty: number; // INTEGER → number
  remaining_qty: number; // INTEGER → number
  unit: string;
}

/**
 * 將符合 C# DraftMenuSession 序列化格式的 JSON 直接寫入 Redis。
 *
 * 目的：模擬 LINE Webhook + LLM 完成草稿建立後的 Redis 狀態，
 *       供 POST /api/menu/publish 測試使用。
 *
 * Redis key 格式：{tenantId}:draft:{lineUserId}:{yyyy-MM-dd}（台灣時區 UTC+8）
 * JSON 命名策略：snake_case_lower（對應後端 JsonNamingPolicy.SnakeCaseLower）
 * TTL：86400 秒（24 小時）
 *
 * ValidationStatus 序列化確認：
 *   後端使用 [JsonConverter(typeof(JsonStringEnumConverter))] + .NET 10 JsonOptions
 *   中 PropertyNamingPolicy = SnakeCaseLower，JsonStringEnumConverter 預設構造器
 *   在 .NET 10 會回退至 options.PropertyNamingPolicy，因此：
 *     ValidationStatus.Ok → "ok"（snake_case_lower）
 *
 * 品項 ID 使用固定值（以 "e2efeed" 前綴辨識為 publish-flow 專用測試資料）：
 *   DRAFT_ITEM_1_ID = "e2efeed000000000000000000000201"（青菜，5 斤）
 *   DRAFT_ITEM_2_ID = "e2efeed000000000000000000000202"（高麗菜，3 顆）
 *
 * @param tenantId   租戶 ID（預設 E2E_TENANT_ID）
 * @param userId     LINE User ID（預設 E2E_LINE_USER_ID）
 * @param today      日期字串，格式 yyyy-MM-dd（預設 todayTaiwan()）
 */
export async function seedDraftToRedis(
  tenantId: string = E2E_TENANT_ID,
  userId: string  = E2E_LINE_USER_ID,
  today: string   = todayTaiwan(),
): Promise<void> {
  const redis = getRedis();
  await redis.connect().catch(() => {/* already connected */});

  const key = `${tenantId}:draft:${userId}:${today}`;
  const now = new Date().toISOString();

  /**
   * DraftMenuSession JSON（snake_case_lower，與後端 RedisDraftSessionStore JsonOptions 一致）
   *
   * 欄位對應：
   *   C# TenantId        → tenant_id
   *   C# LineUserId      → line_user_id
   *   C# Date (DateOnly) → date (string "yyyy-MM-dd")
   *   C# Items           → items
   *   C# CreatedAt       → created_at
   *   C# UpdatedAt       → updated_at
   *
   * DraftItem 欄位對應：
   *   C# Id                  → id
   *   C# Name                → name
   *   C# IsNew               → is_new
   *   C# BuyPrice            → buy_price
   *   C# SellPrice           → sell_price
   *   C# Quantity            → quantity
   *   C# Unit                → unit
   *   C# HistoricalAvgPrice  → historical_avg_price
   *   C# Validation          → validation
   *     C# Status (enum Ok)  → status: "ok"
   *     C# Message           → message: null
   */
  const draftSession = {
    tenant_id:    tenantId,
    line_user_id: userId,
    date:         today,
    items: [
      {
        id:                   'e2efeed000000000000000000000201',
        name:                 'E2E 發布測試青菜',
        is_new:               false,
        buy_price:            100.00,
        sell_price:           150.00,
        quantity:             5,
        unit:                 '斤',
        historical_avg_price: null,
        validation: {
          status:  'ok',
          message: null,
        },
      },
      {
        id:                   'e2efeed000000000000000000000202',
        name:                 'E2E 發布測試高麗菜',
        is_new:               true,
        buy_price:            50.00,
        sell_price:           80.00,
        quantity:             3,
        unit:                 '顆',
        historical_avg_price: null,
        validation: {
          status:  'ok',
          message: null,
        },
      },
    ],
    created_at: now,
    updated_at: now,
  };

  await redis.set(key, JSON.stringify(draftSession), 'EX', 86400);
}

/**
 * 清除指定租戶在 PostgreSQL 與 Redis 中的已發布菜單資料。
 * 供 publish-flow.spec.ts 的 beforeEach 使用，確保每個測試從乾淨的「未發布」狀態出發。
 *
 * DB 清除順序：
 *   1. DELETE FROM published_menu_items WHERE menu_id IN
 *        (SELECT id FROM published_menus WHERE tenant_id = $1)
 *   2. DELETE FROM published_menus WHERE tenant_id = $1
 *   （先刪子表再刪主表，即使 FK 設有 CASCADE 也確保明確清除）
 *
 * Redis 清除：
 *   SCAN + DEL  {tenantId}:menu:published:*
 *   （使用 SCAN 批次清除所有日期的快取，避免遺漏跨日殘留）
 *
 * ⚠️ 不修改 resetAndSeedTestData()，本函式與其獨立使用。
 *
 * @param tenantId  租戶 ID
 */
export async function clearPublishedMenusForTenant(tenantId: string): Promise<void> {
  // ── 1. DB 清除 ────────────────────────────────────────────────────────────
  const client: PoolClient = await getPool().connect();
  try {
    await client.query('BEGIN');

    // 先刪子表（避免 FK 違反，即使有 CASCADE 也明確清除）
    await client.query(
      `DELETE FROM published_menu_items
       WHERE menu_id IN (SELECT id FROM published_menus WHERE tenant_id = $1)`,
      [tenantId],
    );
    // 再刪主表
    await client.query(
      `DELETE FROM published_menus WHERE tenant_id = $1`,
      [tenantId],
    );

    await client.query('COMMIT');
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    client.release();
  }

  // ── 2. Redis 快取清除（SCAN + DEL，避免 write-through 快取污染）─────────
  const redis = getRedis();
  try {
    await redis.connect().catch(() => {/* already connected */});

    const pattern = `${tenantId}:menu:published:*`;
    let cursor = '0';
    do {
      const [nextCursor, keys] = await redis.scan(cursor, 'MATCH', pattern, 'COUNT', 100);
      cursor = nextCursor;
      if (keys.length > 0) {
        // ioredis del 接受單鍵或展開多鍵
        for (const k of keys) {
          await redis.del(k);
        }
      }
    } while (cursor !== '0');
  } catch {
    // Redis 清除失敗時靜默略過，不應阻斷測試（DB 清除已完成）
  }
}

/**
 * 查詢 published_menus 表中符合租戶與日期的記錄。
 * 供 E2E 測試雙重驗證（API 回應 + DB 直查）使用。
 *
 * @param tenantId  租戶 ID
 * @param date      日期字串，格式 yyyy-MM-dd
 * @returns PublishedMenuRow，若不存在回傳 null
 */
export async function getPublishedMenuByTenantAndDate(
  tenantId: string,
  date: string,
): Promise<PublishedMenuRow | null> {
  const result = await getPool().query<PublishedMenuRow>(
    `SELECT * FROM published_menus WHERE tenant_id = $1 AND date = $2`,
    [tenantId, date],
  );
  if (result.rows.length === 0) return null;
  return result.rows[0];
}

/**
 * 查詢 published_menu_items 表中屬於指定菜單的所有品項。
 * 供 E2E 測試雙重驗證（API 回應 + DB 直查）使用。
 *
 * @param menuId  published_menus.id
 * @returns PublishedMenuItemRow 陣列（可能為空）
 */
export async function getPublishedItemsByMenuId(
  menuId: string,
): Promise<PublishedMenuItemRow[]> {
  const result = await getPool().query<PublishedMenuItemRow>(
    `SELECT * FROM published_menu_items WHERE menu_id = $1`,
    [menuId],
  );
  return result.rows;
}

