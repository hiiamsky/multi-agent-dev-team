# Database Migrations

## 概要

此目錄包含 VeggieAlly 專案的資料庫 schema migrations。所有 migration 檔案需按照編號順序手動執行。

## Migration 檔案清單

| 檔案 | 描述 | 狀態 |
|------|------|------|
| `001_create_published_menus.sql` | 建立已發布菜單表與相關索引 | ✅ 基礎 schema |
| `002_ensure_published_menus_unique_constraint.sql` | 確保 UNIQUE constraint 防止並發發布問題 | 🆕 補強約束 |

## 執行指南

### 1. 連線到 PostgreSQL

**使用 Docker Compose (開發環境)**：
```bash
# 啟動服務
docker-compose up -d postgres

# 連線到資料庫容器
docker exec -it veggieally-postgres-1 psql -U veggie -d veggieally
```

**使用 psql 直連**：
```bash
psql -h localhost -p 5432 -U veggie -d veggieally
```

### 2. 執行 Migrations

**方法一：在 psql 內執行**
```sql
-- 執行完整 migration
\i /path/to/001_create_published_menus.sql
\i /path/to/002_ensure_published_menus_unique_constraint.sql
```

**方法二：直接執行檔案**
```bash
# 從專案根目錄執行
docker exec -i veggieally-postgres-1 psql -U veggie -d veggieally < db/migrations/001_create_published_menus.sql
docker exec -i veggieally-postgres-1 psql -U veggie -d veggieally < db/migrations/002_ensure_published_menus_unique_constraint.sql
```

### 3. 驗證執行結果

```sql
-- 檢查表是否存在
\dt published_menus*

-- 檢查 UNIQUE constraint 是否存在
SELECT 
    conname as constraint_name,
    contype as constraint_type,
    pg_get_constraintdef(oid) as definition
FROM pg_constraint 
WHERE conrelid = 'published_menus'::regclass
AND contype = 'u';

-- 預期輸出應該包含：
-- uq_published_menus_tenant_date | u | UNIQUE (tenant_id, date)
```

## 安全性與權限

### 資料庫帳號設計

本專案遵循最小權限原則，建議建立以下專用帳號：

```sql
-- 1. 應用程式讀取帳號
CREATE USER veggie_reader WITH PASSWORD 'your_secure_password';
GRANT CONNECT ON DATABASE veggieally TO veggie_reader;
GRANT USAGE ON SCHEMA public TO veggie_reader;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO veggie_reader;

-- 2. 應用程式寫入帳號  
CREATE USER veggie_writer WITH PASSWORD 'your_secure_password';
GRANT CONNECT ON DATABASE veggieally TO veggie_writer;
GRANT USAGE ON SCHEMA public TO veggie_writer;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO veggie_writer;

-- 3. Migration 帳號（DDL 權限）
CREATE USER veggie_migrator WITH PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE veggieally TO veggie_migrator;
```

### 連線字串配置

更新 `appsettings.json` 使用專用帳號：

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=veggieally;Username=veggie_writer;Password=your_secure_password"
  }
}
```

## 並發問題解決方案

### C-1: PublishMenuHandler 並發發布

**問題**：兩個並發請求可能同時通過 Redis cache 檢查，造成重複插入。

**解決方案**：
1. ✅ **DB 層防護**：`002_ensure_published_menus_unique_constraint.sql` 確保 UNIQUE constraint 存在
2. ✅ **應用層處理**：`PublishedMenuRepository.InsertAsync()` 已處理 UNIQUE violation (SQLSTATE 23505)

**驗證並發防護**：
```sql
-- 測試重複插入（應該失敗）
INSERT INTO published_menus (id, tenant_id, published_by, date, published_at) 
VALUES ('test1', 'tenant1', 'user1', '2024-12-19', NOW());

INSERT INTO published_menus (id, tenant_id, published_by, date, published_at) 
VALUES ('test2', 'tenant1', 'user2', '2024-12-19', NOW());
-- 第二條應該回傳: ERROR: duplicate key value violates unique constraint
```

## 效能考量

### 索引策略

現有索引配置：
- `uq_published_menus_tenant_date` (UNIQUE) → 自動建立 B-tree 索引，支援 (tenant_id, date) 查詢
- `idx_published_menus_tenant_date` → 可能重複，建議評估移除
- `idx_published_menus_published_at` → 支援時間範圍查詢

### 查詢優化檢查

```sql
-- 檢查常用查詢的執行計劃
EXPLAIN (ANALYZE, BUFFERS) 
SELECT * FROM published_menus 
WHERE tenant_id = 'tenant1' AND date = '2024-12-19';
```

預期應看到：`Index Scan using uq_published_menus_tenant_date`，而非 `Seq Scan`。

## 故障排除

### 常見問題

1. **Migration 檔案不存在**：確認檔案路徑正確，使用絕對路徑
2. **權限不足**：確認使用的帳號具備 DDL 權限
3. **Constraint 衝突**：若已有重複資料，需先清理再執行 migration
4. **容器連線問題**：確認 PostgreSQL 容器正在運行且端口正確

### 資料清理 (僅開發環境)

```sql
-- 清理測試資料 (謹慎使用！)
TRUNCATE published_menu_items, published_menus RESTART IDENTITY CASCADE;
```