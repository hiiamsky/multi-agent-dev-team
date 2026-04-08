-- P3-002: 確保 published_menus 唯一性約束存在
-- 目的：修復可能遺漏的 UNIQUE constraint，防止並發發布造成重複記錄

-- ============================================================================
-- 檢查並補強 UNIQUE constraint
-- ============================================================================

-- 方法一：嘗試添加 constraint（若已存在會被忽略）
DO $$
BEGIN
    -- 嘗試添加 UNIQUE constraint
    BEGIN
        ALTER TABLE published_menus 
        ADD CONSTRAINT uq_published_menus_tenant_date 
        UNIQUE (tenant_id, date);
        
        RAISE NOTICE 'UNIQUE constraint uq_published_menus_tenant_date 已成功添加';
    EXCEPTION 
        WHEN duplicate_object THEN
            RAISE NOTICE 'UNIQUE constraint uq_published_menus_tenant_date 已存在，跳過添加';
        WHEN OTHERS THEN
            RAISE EXCEPTION '添加 UNIQUE constraint 時發生錯誤: %', SQLERRM;
    END;
END $$;

-- ============================================================================
-- 驗證 constraint 存在性
-- ============================================================================

-- 查詢並顯示 constraint 資訊以供驗證
DO $$
DECLARE
    constraint_exists BOOLEAN := FALSE;
BEGIN
    SELECT EXISTS (
        SELECT 1 
        FROM information_schema.constraint_column_usage 
        WHERE table_name = 'published_menus' 
        AND constraint_name = 'uq_published_menus_tenant_date'
    ) INTO constraint_exists;
    
    IF constraint_exists THEN
        RAISE NOTICE '✓ UNIQUE constraint 驗證通過: uq_published_menus_tenant_date 存在於 published_menus 表';
    ELSE
        RAISE EXCEPTION '✗ UNIQUE constraint 驗證失敗: uq_published_menus_tenant_date 不存在';
    END IF;
END $$;

-- ============================================================================
-- 索引優化檢查
-- ============================================================================

-- PostgreSQL 會自動為 UNIQUE constraint 建立對應索引，但我們檢查一下是否需要額外優化
DO $$
BEGIN
    -- 檢查是否存在支援 (tenant_id, date) 查詢的索引
    IF EXISTS (
        SELECT 1 
        FROM pg_indexes 
        WHERE tablename = 'published_menus' 
        AND (indexname LIKE '%tenant%date%' OR indexname LIKE '%uq_published%')
    ) THEN
        RAISE NOTICE '✓ 索引驗證通過: published_menus 表具備 (tenant_id, date) 索引支援';
    ELSE
        RAISE WARNING '⚠ 索引建議: 考慮檢查是否需要額外的查詢索引';
    END IF;
END $$;

-- ============================================================================
-- Migration 記錄
-- ============================================================================

COMMENT ON TABLE published_menus IS 
'已發布菜單主表 - 每日每租戶一筆記錄。Migration 002: 確保 UNIQUE constraint 存在以防止並發發布問題';