# Phase 1 端對端驗收測試

## 前置作業

### 1. 設定 User Secrets

```bash
cd VeggieAlly/src/VeggieAlly.WebAPI

# LINE Messaging API（LINE Developers Console → 你的 Channel）
dotnet user-secrets set "Line:ChannelSecret" "<你的 Channel Secret>"
dotnet user-secrets set "Line:ChannelAccessToken" "<你的 Channel Access Token>"

# Gemini API（Google AI Studio → Get API Key）
dotnet user-secrets set "Gemini:ApiKey" "<你的 Gemini API Key>"
```

### 2. 安裝 ngrok（如尚未安裝）

```bash
brew install ngrok
ngrok config add-authtoken <你的 ngrok authtoken>
```

### 3. 啟動服務

```bash
# Terminal 1: 啟動 WebAPI
cd VeggieAlly/src/VeggieAlly.WebAPI
dotnet run --launch-profile http
# → http://localhost:5273

# Terminal 2: 啟動 ngrok 隧道
ngrok http 5273
# → 複製 Forwarding URL，例如 https://xxxx.ngrok-free.app
```

### 4. 設定 LINE Webhook URL

LINE Developers Console → Messaging API：
- Webhook URL：`https://xxxx.ngrok-free.app/api/webhook`
- 開啟「Use webhook」
- 點「Verify」應回傳 200 OK

---

## 測試向量（10+ 筆）

### 正常案例

| # | 輸入文字 | 預期品項 | 關鍵驗證 |
|---|---------|---------|---------|
| 1 | `高麗菜 25 賣 35 五十箱` | 初秋高麗菜, buy=25, sell=35, qty=50 | 口語品名→子品種、中文數字 |
| 2 | `小白菜 15 三十箱 空心菜 12 二十箱` | 2 筆品項 | 多品項連續輸入 |
| 3 | `紅蘿蔔 進18 售30 一百箱` | buy=18, sell=30, qty=100 | 「進」「售」關鍵字 |
| 4 | `牛番茄 22 40箱` | buy=22, sell=0, qty=40 | 單價格預設為 buy_price |
| 5 | `初秋高麗菜 28 五十箱 改良高麗菜 30 三十箱` | 2 筆子品種 | 精確子品種名配對 |
| 6 | `本地洋蔥 成本20 賣28 六十箱` | buy=20, sell=28, qty=60 | 「成本」關鍵字 |
| 7 | `杏鮑菇 45 十箱 金針菇 18 二十箱 秀珍菇 35 十五箱` | 3 筆蕈菇 | 三品項解析 |

### 邊界案例

| # | 輸入文字 | 預期行為 | 關鍵驗證 |
|---|---------|---------|---------|
| 8 | `有機蘆筍 55 十箱` | is_new=true | 不在清單中→新品 |
| 9 | `高麗菜 25` | qty=0（未提供數量） | 缺少數量欄位 |
| 10 | `今天不進貨` | 合理 JSON（items 空陣列或錯誤提示） | 非報價文字 |

### 異常案例

| # | 輸入文字 | 預期行為 | 關鍵驗證 |
|---|---------|---------|---------|
| 11 | `hello` | JSON 或錯誤提示 | 英文 / 無意義文字 |
| 12 | ` `（空白） | Handler 跳過，不回覆 | 空文字防護 |
| 13 | `白蘿蔔 999999 一億箱` | 合理 JSON（極大數字） | 極端數值 |

---

## 驗收記錄表

| # | 通過? | 回應時間 | 回傳 JSON 正確? | 備註 |
|---|:----:|--------:|:--------------:|------|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |
| 4 | | | | |
| 5 | | | | |
| 6 | | | | |
| 7 | | | | |
| 8 | | | | |
| 9 | | | | |
| 10 | | | | |
| 11 | | | | |
| 12 | | | | |
| 13 | | | | |

## 驗收標準

| 指標 | 目標 | 結果 |
|------|------|------|
| LINE App → Bot 回覆 JSON | 可運作 | |
| P95 回應時間 | < 5 秒 | |
| 正確解析率（#1~#10） | ≥ 80%（≥8/10） | |
| Unhandled Exception | 0 | |
