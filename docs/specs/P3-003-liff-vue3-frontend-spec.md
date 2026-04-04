# P3-003：LIFF 前端規格書（Vue 3）

**版本**：v1.0  
**日期**：2026-04-04  
**Issue**：#18  
**依賴**：P3-001（PATCH 修正 API）、P3-002（今日菜單 API + 庫存 API）

---

## 1. ADR（架構決策紀錄）

### ADR-1：框架選型 — Vue 3 + Vite + TypeScript
**決策**：Vue 3 Composition API（`<script setup lang="ts">`）+ Vite 6 + TypeScript  
**理由**：PRD 明確指定 Vue 3；Vite 零配置快速啟動；TypeScript 確保 API 契約型別安全  

### ADR-2：LIFF SDK 初始化策略 — App-level 全域一次
**決策**：在 `main.ts` 呼叫 `liff.init()` 完成後才掛載 Vue App  
**理由**：所有頁面都需要 access token，提前初始化避免重複呼叫；失敗時顯示全域錯誤頁面

### ADR-3：狀態管理 — 無 Pinia，純 Composables
**決策**：使用 Vue 3 `reactive`/`ref` + composables，不引入 Pinia  
**理由**：只有 2 個頁面，無跨頁共享狀態需求；composables 足夠且減少依賴

### ADR-4：CSS 方案 — 純 CSS Custom Properties
**決策**：不使用 Tailwind，純 CSS + CSS variables  
**理由**：頁面少、樣式簡單；避免引入 Tailwind 增加 bundle size；LIFF 內嵌需要極小 payload

### ADR-5：路由方案 — Vue Router Hash Mode
**決策**：Vue Router 4，hash mode（`createWebHashHistory()`）  
**理由**：LIFF 應用跑在 LINE WebView，hash mode 避免伺服器配置需求

### ADR-6：API 呼叫封裝 — fetch wrapper
**決策**：原生 `fetch` + 自建 `useApi` composable  
**理由**：避免引入 axios；fetch 已足夠；composable 封裝 Bearer token 注入與錯誤處理

### ADR-7：部署方案 — Vite build → .NET wwwroot
**決策**：`vite build` 輸出到 `VeggieAlly/src/VeggieAlly.WebAPI/wwwroot/liff/`，由 ASP.NET Static Files middleware serve  
**理由**：單一 container 部署；不需獨立 nginx；CORS 問題天然避免（同源）

---

## 2. 頁面規格

### 2.1 NumPadPage（智慧數字鍵盤）

**路由**：`/#/numpad?item_id={id}&field={buy_price|sell_price}`  
**來源**：Flex Message 中 anomaly 品項的「修正」按鈕（LIFF URI action）

#### 頁面佈局
```
┌─────────────────────────┐
│  🔧 修正價格             │  ← Header
│  品名：高麗菜            │
│  欄位：進貨價             │
│  目前：$25               │
├─────────────────────────┤
│  ┌──────────────────┐   │
│  │     $ 0           │   │  ← 價格顯示區
│  └──────────────────┘   │
│                         │
│  ┌────┬────┬────┐      │
│  │ 1  │ 2  │ 3  │      │
│  ├────┼────┼────┤      │
│  │ 4  │ 5  │ 6  │      │
│  ├────┼────┼────┤      │
│  │ 7  │ 8  │ 9  │      │
│  ├────┼────┼────┤      │
│  │ 00 │ 0  │ ⌫  │      │  ← 80×80px buttons
│  └────┴────┴────┘      │
│                         │
│  ┌─────────────────┐   │
│  │   ✅ 確認送出     │   │  ← 送出按鈕（綠色）
│  └─────────────────┘   │
│                         │
│  ┌─────────────────┐   │
│  │   ❌ 取消         │   │  ← 取消（關閉 LIFF）
│  └─────────────────┘   │
└─────────────────────────┘
```

#### 行為規格
| 操作 | 行為 |
|------|------|
| 數字鍵 0-9 | 附加到輸入值尾部 |
| `00` 鍵 | 附加兩個零（快速輸入整數價格如 1500） |
| `⌫` 鍵 | 刪除最後一個字元；空時不動作 |
| `.` 鍵 | 不提供：PRD 品項單價皆為整數（$25, $35） |
| 確認送出 | 值 > 0 時 PATCH API → 成功顯示 ✅ toast → 1 秒後 `liff.closeWindow()` |
| 取消 | `liff.closeWindow()` |
| 值為 0 | 送出按鈕 disabled |
| 超過 5 位數 | 不再附加（最大 99999） |

#### QueryString 驗證
| 參數 | 驗證規則 | 失敗處理 |
|------|---------|---------|
| `item_id` | 32-char hex (`/^[0-9a-f]{32}$/`) | 顯示「無效的品項 ID」→ 關閉按鈕 |
| `field` | `buy_price` 或 `sell_price` | 顯示「無效的欄位」→ 關閉按鈕 |

#### API 呼叫
```
PATCH /api/draft/item/{item_id}
Authorization: Bearer {liff_access_token}
Content-Type: application/json

// field === 'buy_price':
{ "buy_price": 25 }

// field === 'sell_price':
{ "sell_price": 35 }
```

### 2.2 TodayMenuPage（銷售端今日菜單）

**路由**：`/#/menu`  
**來源**：LINE 訊息中的 LIFF Link 或獨立分享的 URL

#### 頁面佈局
```
┌─────────────────────────┐
│  🥬 今日菜單  2026/04/04 │  ← Header + 日期
│  發布者：王老闆           │
│  共 12 個品項             │
├─────────────────────────┤
│  ┌─────────────────────┐│
│  │ 高麗菜      $35/箱  ││
│  │ 庫存：48/50         ││
│  │ 下單：[-] 0 [+]     ││  ← 數量選擇器
│  └─────────────────────┘│
│  ┌─────────────────────┐│
│  │ 白蘿蔔      $30/箱  ││
│  │ 庫存：30/30         ││
│  │ 下單：[-] 0 [+]     ││
│  └─────────────────────┘│
│  ... (可捲動)            │
├─────────────────────────┤
│  合計：0 項  $0          │  ← 底部固定
│  ┌─────────────────┐   │
│  │   🛒 送出訂單     │   │  ← disabled when total=0
│  └─────────────────┘   │
└─────────────────────────┘
```

#### 行為規格
| 操作 | 行為 |
|------|------|
| 載入 | `GET /api/menu/today?tenant_id={tenantId}` → 渲染列表 |
| `+` 按鈕 | 數量 +1，最大不超過 remaining_qty |
| `-` 按鈕 | 數量 -1，最小 0 |
| 送出訂單 | 逐品項呼叫 `PATCH /api/menu/inventory`（amount > 0 的品項） |
| 庫存不足(409) | toast 提示「XX 庫存不足」，重新載入菜單 |
| 菜單未發布(404) | 顯示「今日菜單尚未發布」空狀態 |
| 下單成功 | 顯示 ✅ 成功 toast → 重新載入更新庫存 |

#### 下單流程（序列呼叫，非並行）
```
for each item where orderQty > 0:
  PATCH /api/menu/inventory
  Header: X-Tenant-Id: {tenantId}
  Authorization: Bearer {token}
  Body: { "item_id": item.id, "amount": orderQty }
  
  if 409 → 中止剩餘，提示庫存不足
  if 200 → 繼續下一品項

全部完成 → 成功提示 → 重新載入
```

---

## 3. 元件樹

```
liff-app/
├── index.html
├── package.json
├── tsconfig.json
├── vite.config.ts
├── env.d.ts
├── .env.example                    # VITE_API_BASE_URL, VITE_LIFF_ID
├── src/
│   ├── main.ts                     # LIFF init → createApp
│   ├── App.vue                     # <router-view />
│   ├── router.ts                   # hash mode routes
│   ├── types/
│   │   └── api.ts                  # API request/response 型別
│   ├── composables/
│   │   ├── useLiff.ts              # LIFF SDK wrapper（token, userId, close）
│   │   └── useApi.ts               # fetch wrapper（auth header, error handling）
│   ├── pages/
│   │   ├── NumPadPage.vue          # 數字鍵盤頁面
│   │   └── TodayMenuPage.vue       # 今日菜單頁面
│   ├── components/
│   │   ├── NumPad.vue              # 數字鍵盤元件（emit: input, backspace）
│   │   ├── MenuItem.vue            # 單一品項卡片（props: item, emit: qty-change）
│   │   ├── Toast.vue               # 全域 toast 提示
│   │   └── ErrorPage.vue           # LIFF 初始化失敗 / 參數錯誤
│   └── styles/
│       └── global.css              # CSS variables, reset, 共用樣式
└── vitest.config.ts
```

---

## 4. LIFF 初始化流程

```
sequenceDiagram
    participant U as 使用者(LINE)
    participant L as LIFF WebView
    participant SDK as LIFF SDK
    participant API as VeggieAlly API
    participant LINE as LINE Platform

    U->>L: 點擊 Flex Message 修正按鈕
    L->>SDK: liff.init({ liffId })
    SDK->>LINE: OAuth 驗證
    LINE-->>SDK: Access Token
    SDK-->>L: init 成功
    L->>L: liff.getAccessToken()
    L->>API: PATCH /api/draft/item/{id}<br>Authorization: Bearer {token}
    API->>LINE: GET /oauth2/v2.1/verify
    LINE-->>API: Token 有效
    API->>LINE: GET /v2/profile
    LINE-->>API: userId, displayName
    API-->>L: 200 OK { validation result }
    L->>L: 顯示成功 → liff.closeWindow()
```

---

## 5. 型別定義

```typescript
// types/api.ts

// === Draft 修正 ===
export interface CorrectPriceRequest {
  buy_price?: number
  sell_price?: number
}

export interface CorrectPriceResponse {
  id: string
  name: string
  is_new: boolean
  buy_price: number
  sell_price: number
  quantity: number
  unit: string
  historical_avg_price: number | null
  validation: {
    status: 'passed' | 'failed' | 'warning'
    message: string | null
  }
}

// === 今日菜單 ===
export interface TodayMenuResponse {
  id: string
  tenant_id: string
  date: string
  published_at: string
  items: MenuItemResponse[]
}

export interface MenuItemResponse {
  id: string
  name: string
  is_new: boolean
  sell_price: number
  original_qty: number
  remaining_qty: number
  unit: string
}

// === 庫存扣除 ===
export interface DeductInventoryRequest {
  item_id: string
  amount: number
}

export interface DeductInventoryResponse {
  id: string
  name: string
  remaining_qty: number
  unit: string
}

// === 通用錯誤 ===
export interface ApiError {
  error: {
    code: string
    message: string
  }
}
```

---

## 6. Composables 規格

### 6.1 useLiff.ts

```typescript
export function useLiff() {
  const isReady: Ref<boolean>
  const accessToken: Ref<string | null>
  const userId: Ref<string | null>
  const error: Ref<string | null>

  async function init(liffId: string): Promise<void>
  function getToken(): string   // throws if not ready
  function closeWindow(): void
}
```

### 6.2 useApi.ts

```typescript
export function useApi(getToken: () => string) {
  const loading: Ref<boolean>
  const error: Ref<ApiError | null>

  async function patch<T>(path: string, body: object): Promise<T>
  async function get<T>(path: string, params?: Record<string, string>): Promise<T>
}
```

**內建行為**：
- 自動附加 `Authorization: Bearer {token}`
- `Content-Type: application/json`
- HTTP 狀態非 2xx → 解析 body 為 `ApiError` 並 throw
- loading reactive flag

---

## 7. 安全設計

| 項目 | 措施 |
|------|------|
| XSS | Vue 3 預設 escape + 不使用 `v-html` |
| Token 儲存 | LIFF SDK 記憶體內管理，不存 localStorage/sessionStorage |
| QueryString 驗證 | `item_id` regex `/^[0-9a-f]{32}$/`，`field` 白名單 |
| CORS | 同源（wwwroot 同功能變數名稱），不需額外 CORS header |
| 輸入驗證 | 數字鍵盤只允許 0-9 + 00，後端也會驗證 |
| CSP | `<meta>` tag 限制 script-src / connect-src |

---

## 8. 錯誤處理策略

| 場景 | 處理 |
|------|------|
| LIFF init 失敗 | ErrorPage 顯示「請在 LINE 中開啟此頁面」 |
| Token 為 null | ErrorPage 顯示「無法取得驗證資訊」 |
| API 401 | Toast「登入已過期，請重新開啟」→ 1.5s → closeWindow |
| API 400 | Toast 顯示 error.message |
| API 404 | 頁面顯示空狀態提示 |
| API 409 | Toast「庫存不足」→ 重新載入 |
| 網路斷線 | Toast「網路連線異常，請稍後再試」 |
| QueryString 無效 | NumPadPage → ErrorPage 帶錯誤訊息 |

---

## 9. 後端 CORS / Static Files 配置

在 `Program.cs` 新增：
```csharp
app.UseStaticFiles(); // serves wwwroot/ including wwwroot/liff/
```

Vite build output 配置 → `outDir: '../../src/VeggieAlly.WebAPI/wwwroot/liff'`

LIFF App URL 設為：`https://{domain}/liff/index.html`

---

## 10. 測試計劃

### 10.1 NumPad 元件測試（Vitest + @vue/test-utils）

| # | 測試名稱 | 預期行為 |
|---|---------|---------|
| 1 | `digit_appends_to_value` | 按 1,2,3 → 顯示 "123" |
| 2 | `double_zero_appends` | 按 1,0,0 → 顯示 "100" |
| 3 | `backspace_removes_last` | "123" → ⌫ → "12" |
| 4 | `backspace_empty_noop` | "" → ⌫ → "" |
| 5 | `max_5_digits` | "99999" → 按 1 → 仍為 "99999" |
| 6 | `double_zero_on_empty_stays_zero` | "" → 00 → "0"（不顯示 "00"） |
| 7 | `confirm_disabled_when_zero` | 值為 "0" → 確認按鈕 disabled |

### 10.2 NumPadPage 測試

| # | 測試名稱 | 預期行為 |
|---|---------|---------|
| 1 | `renders_item_info_from_query` | 顯示品名、欄位、當前價格 |
| 2 | `invalid_item_id_shows_error` | item_id 非 32-hex → ErrorPage |
| 3 | `invalid_field_shows_error` | field 非 buy_price/sell_price → ErrorPage |
| 4 | `submit_calls_patch_api` | 確認 → PATCH 被呼叫，body 含正確 field |
| 5 | `submit_success_shows_toast_and_closes` | API 200 → toast → closeWindow |
| 6 | `submit_401_shows_expired` | API 401 → 顯示過期訊息 |
| 7 | `submit_404_shows_not_found` | API 404 → 顯示找不到品項 |
| 8 | `cancel_closes_liff` | 按取消 → closeWindow 被呼叫 |

### 10.3 TodayMenuPage 測試

| # | 測試名稱 | 預期行為 |
|---|---------|---------|
| 1 | `loads_and_renders_menu` | GET 200 → 列表渲染全部品項 |
| 2 | `404_shows_empty_state` | GET 404 → 顯示「今日尚未發布」 |
| 3 | `increment_qty_within_stock` | +按鈕 → 數量+1，不超過 remaining |
| 4 | `decrement_qty_to_zero` | -按鈕 → 數量-1，最小 0 |
| 5 | `order_button_disabled_when_no_items` | 全部 qty=0 → 按鈕 disabled |
| 6 | `submit_order_calls_inventory_api` | 送出 → 每個 qty>0 品項呼叫 PATCH |
| 7 | `order_409_shows_stock_error` | 庫存不足 → toast 提示+重載 |
| 8 | `order_success_reloads_menu` | 全部成功 → 重新載入更新庫存 |

### 10.4 useApi Composable 測試

| # | 測試名稱 | 預期行為 |
|---|---------|---------|
| 1 | `attaches_bearer_token` | 請求含 Authorization header |
| 2 | `parses_json_response` | 200 → 回傳 parsed body |
| 3 | `throws_api_error_on_4xx` | 400 → throw ApiError |
| 4 | `sets_loading_during_request` | request 中 loading=true → 完成 false |
| 5 | `handles_network_error` | fetch throws → 包裝為錯誤 |

### 10.5 useLiff Composable 測試

| # | 測試名稱 | 預期行為 |
|---|---------|---------|
| 1 | `init_success_sets_ready` | liff.init 成功 → isReady=true |
| 2 | `init_failure_sets_error` | liff.init 失敗 → error 有訊息 |
| 3 | `getToken_returns_access_token` | isReady 後 → 回傳 token |
| 4 | `getToken_throws_if_not_ready` | init 前呼叫 → throw |

**測試總數：32 個**

---

## 11. 交付檔案清單

| # | 路徑 | 說明 |
|---|------|------|
| 1 | `VeggieAlly/liff-app/package.json` | NPM 設定 |
| 2 | `VeggieAlly/liff-app/tsconfig.json` | TypeScript 設定 |
| 3 | `VeggieAlly/liff-app/vite.config.ts` | Vite 設定 + build output |
| 4 | `VeggieAlly/liff-app/vitest.config.ts` | Vitest 設定 |
| 5 | `VeggieAlly/liff-app/index.html` | SPA entry |
| 6 | `VeggieAlly/liff-app/.env.example` | 環境變數範本 |
| 7 | `VeggieAlly/liff-app/env.d.ts` | Vite env 型別宣告 |
| 8 | `VeggieAlly/liff-app/src/main.ts` | LIFF init + Vue mount |
| 9 | `VeggieAlly/liff-app/src/App.vue` | Root component |
| 10 | `VeggieAlly/liff-app/src/router.ts` | Vue Router hash mode |
| 11 | `VeggieAlly/liff-app/src/types/api.ts` | API 型別 |
| 12 | `VeggieAlly/liff-app/src/composables/useLiff.ts` | LIFF SDK wrapper |
| 13 | `VeggieAlly/liff-app/src/composables/useApi.ts` | Fetch wrapper |
| 14 | `VeggieAlly/liff-app/src/pages/NumPadPage.vue` | 數字鍵盤頁面 |
| 15 | `VeggieAlly/liff-app/src/pages/TodayMenuPage.vue` | 今日菜單頁面 |
| 16 | `VeggieAlly/liff-app/src/components/NumPad.vue` | 數字鍵盤元件 |
| 17 | `VeggieAlly/liff-app/src/components/MenuItem.vue` | 品項卡片元件 |
| 18 | `VeggieAlly/liff-app/src/components/Toast.vue` | Toast 提示 |
| 19 | `VeggieAlly/liff-app/src/components/ErrorPage.vue` | 錯誤頁面 |
| 20 | `VeggieAlly/liff-app/src/styles/global.css` | 全域樣式 |
| 21 | `VeggieAlly/liff-app/src/__tests__/NumPad.test.ts` | NumPad 測試 |
| 22 | `VeggieAlly/liff-app/src/__tests__/NumPadPage.test.ts` | NumPadPage 測試 |
| 23 | `VeggieAlly/liff-app/src/__tests__/TodayMenuPage.test.ts` | TodayMenuPage 測試 |
| 24 | `VeggieAlly/liff-app/src/__tests__/useApi.test.ts` | useApi 測試 |
| 25 | `VeggieAlly/liff-app/src/__tests__/useLiff.test.ts` | useLiff 測試 |
