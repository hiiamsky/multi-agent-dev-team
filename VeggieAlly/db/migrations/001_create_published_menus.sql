-- P3-002: 一鍵發布鎖定 + 今日菜單 API + 庫存扣除
-- 建立已發布菜單相關資料表

-- 已發布菜單主表
CREATE TABLE IF NOT EXISTS published_menus (
    id              VARCHAR(32)     PRIMARY KEY,           -- GUID "N" 格式
    tenant_id       VARCHAR(64)     NOT NULL,              -- 租戶 ID
    published_by    VARCHAR(64)     NOT NULL,              -- 發布者 LINE User ID
    date            DATE            NOT NULL,              -- 菜單日期
    published_at    TIMESTAMPTZ     NOT NULL DEFAULT NOW(),-- 發布時間 (UTC)
    
    -- 確保每個租戶每天只能發布一次
    CONSTRAINT uq_published_menus_tenant_date UNIQUE (tenant_id, date)
);

-- 索引
CREATE INDEX IF NOT EXISTS idx_published_menus_tenant_date ON published_menus (tenant_id, date);
CREATE INDEX IF NOT EXISTS idx_published_menus_published_at ON published_menus (published_at);

-- 已發布菜單品項表
CREATE TABLE IF NOT EXISTS published_menu_items (
    id                  VARCHAR(32)     PRIMARY KEY,       -- GUID "N" 格式
    menu_id             VARCHAR(32)     NOT NULL REFERENCES published_menus(id) ON DELETE CASCADE,
    tenant_id           VARCHAR(64)     NOT NULL,          -- 冗余儲存租戶 ID，便於查詢優化
    name                VARCHAR(128)    NOT NULL,          -- 品項名稱
    is_new              BOOLEAN         NOT NULL DEFAULT FALSE, -- 是否新品
    buy_price           DECIMAL(10,2)   NOT NULL,          -- 進貨價
    sell_price          DECIMAL(10,2)   NOT NULL,          -- 售價
    original_qty        INT             NOT NULL,          -- 原始數量
    remaining_qty       INT             NOT NULL,          -- 剩餘庫存 
    unit                VARCHAR(16)     NOT NULL,          -- 單位
    historical_avg_price DECIMAL(10,2),                   -- 歷史平均價（可選）
    
    -- 確保庫存不會是負數
    CONSTRAINT chk_remaining_qty CHECK (remaining_qty >= 0),
    CONSTRAINT chk_original_qty CHECK (original_qty >= 0),
    CONSTRAINT chk_prices CHECK (buy_price >= 0 AND sell_price >= 0)
);

-- 索引
CREATE INDEX IF NOT EXISTS idx_published_menu_items_menu_id ON published_menu_items (menu_id);
CREATE INDEX IF NOT EXISTS idx_published_menu_items_tenant ON published_menu_items (tenant_id);
CREATE INDEX IF NOT EXISTS idx_published_menu_items_name ON published_menu_items (name);

-- 建立註解
COMMENT ON TABLE published_menus IS '已發布菜單主表 - 每日每租戶一筆記錄';
COMMENT ON TABLE published_menu_items IS '已發布菜單品項表 - 包含庫存管理';

COMMENT ON COLUMN published_menus.id IS 'GUID "N" 格式唯一識別碼';
COMMENT ON COLUMN published_menus.tenant_id IS '租戶識別碼，用於多租戶隔離';
COMMENT ON COLUMN published_menus.published_by IS '發布者 LINE User ID';
COMMENT ON COLUMN published_menus.date IS '菜單適用日期（以台灣時間為準）';
COMMENT ON COLUMN published_menus.published_at IS 'UTC 發布時間戳';

COMMENT ON COLUMN published_menu_items.remaining_qty IS '目前剩餘庫存，會因銷售而遞減';
COMMENT ON COLUMN published_menu_items.original_qty IS '原始發布數量，不變';
COMMENT ON COLUMN published_menu_items.tenant_id IS '冗余儲存，避免跨表JOIN查詢';