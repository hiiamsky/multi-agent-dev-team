# VeggieAlly 菜商神隊友

> 蔬菜批發商的 LINE 智能助理——透過語音/圖片辨識自動建立每日菜單，並提供 LIFF 銷售端讓採購商即時查價、下單扣庫存。

---

## 技術棧

| 層次 | 技術 |
|------|------|
| 後端框架 | ASP.NET Core Web API / **.NET 10** |
| 架構模式 | **Clean Architecture + CQRS**（MediatR） |
| ORM | **Dapper**（Repository Pattern，手寫 SQL） |
| AI 抽象 | Microsoft.Extensions.AI（`IChatClient`）+ Semantic Kernel Plugins |
| AI 服務 | **Gemini 2.0 Flash**（語音/圖像解析），OpenAI Whisper 備援 |
| 前端 | **Vue 3** + LINE LIFF SDK |
| 主資料庫 | **PostgreSQL 16** |
| 快取 | **Redis 7**（當日草稿菜單、已發布菜單） |
| 部署 | Docker + GitHub Actions |

---

## 系統架構

```
VeggieAlly.WebAPI        ← Controllers、Filters（LINE Signature / LIFF Auth）、Program.cs
VeggieAlly.Application   ← Command/Query Handlers（CQRS）、業務流程、AI Plugin 呼叫
VeggieAlly.Domain        ← 領域模型、介面定義（無外向依賴）
VeggieAlly.Infrastructure ← Dapper Repositories、Redis、Gemini/OpenAI 整合
```

LINE Chatbot（銷售端）→ Webhook → `WebhookController` → MediatR Handler → Domain / AI  
LINE LIFF（採購端）→ REST API → `MenuController` / `InventoryController` → MediatR Handler

---

## 快速啟動（開發環境）

### 前置需求

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) ≥ 24
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Node.js ≥ 20（前端開發）

### 1. 啟動基礎服務（PostgreSQL + Redis）

```bash
docker compose up -d postgres redis
```

> 若要一併啟動容器化 API：`docker compose up -d`（API 位於 http://localhost:5010）

### 2. 設定環境變數

複製並填入必要設定：

```bash
# 建立本機覆寫設定（不要 commit）
cp src/VeggieAlly.WebAPI/appsettings.json src/VeggieAlly.WebAPI/appsettings.Development.json
```

編輯 `appsettings.Development.json`，填入以下欄位（參考下方「環境變數」章節）。

### 3. 啟動 API

```bash
cd src/VeggieAlly.WebAPI
dotnet run
```

API 預設於 `http://localhost:5010`，Swagger UI 位於 `http://localhost:5010/docs`。

### 4. 啟動前端（LIFF App）

```bash
cd liff-app
npm install
npm run dev
```

---

## 資料庫 Migration

Migration SQL 位於 `db/migrations/`，需手動依序執行：

```bash
# 範例（使用 psql）
psql -h localhost -U veggie -d veggieally \
  -f db/migrations/001_create_published_menus.sql \
  -f db/migrations/002_ensure_published_menus_unique_constraint.sql
```

> 密碼預設為 `veggie_dev`（僅限本機開發，正式環境請替換）。

---

## 環境變數 / 設定

| 設定鍵 | 說明 | 範例 |
|--------|------|------|
| `ConnectionStrings__PostgreSQL` | PostgreSQL 連線字串 | `Host=localhost;Database=veggieally;Username=veggie;Password=...` |
| `Redis__ConnectionString` | Redis 位址 | `localhost:6379` |
| `Line__ChannelSecret` | LINE Bot Channel Secret | *(勿洩漏)* |
| `Line__ChannelAccessToken` | LINE Bot Access Token | *(勿洩漏)* |
| `Line__LiffBaseUrl` | LIFF 前端網址 | `https://your-liff-domain.com` |
| `Line__TenantId` | 租戶識別碼 | `default` |
| `Gemini__ApiKey` | Gemini API Key | *(勿洩漏)* |
| `Gemini__ModelId` | Gemini 模型 | `gemini-2.0-flash` |
| `AI__Provider` | AI 提供者（本機開發） | `ollama` |

Docker Compose 透過 Shell 環境變數傳入，建議建立 `.env` 檔（已加入 `.gitignore`）：

```bash
LINE_CHANNEL_SECRET=your_secret
LINE_CHANNEL_ACCESS_TOKEN=your_token
LIFF_BASE_URL=https://your-liff-domain.com
GEMINI_API_KEY=your_key
```

---

## 測試

### 單元測試

```bash
dotnet test tests/VeggieAlly.Application.Tests/
```

### E2E 測試（Playwright）

> 前提：先啟動 `docker compose -f docker-compose.test.yml up -d`

```bash
cd tests/e2e
npm install
npm test                  # 循序執行所有 API E2E 測試
npm run test:ui           # 互動式 UI 模式
npm run test:report       # 開啟 HTML 報告
```

測試目標：`http://localhost:5010`（可用 `API_BASE_URL` 環境變數覆寫）

---

## 專案目錄結構

```
VeggieAlly/
├── src/
│   ├── VeggieAlly.WebAPI/         # Controllers、Filters、Program.cs
│   ├── VeggieAlly.Application/    # CQRS Handlers、AI Plugin 呼叫
│   ├── VeggieAlly.Domain/         # 領域模型、介面
│   └── VeggieAlly.Infrastructure/ # DB、Redis、AI 整合實作
├── tests/
│   ├── VeggieAlly.Application.Tests/  # xUnit 單元測試
│   └── e2e/                           # Playwright API E2E 測試
├── db/
│   └── migrations/                # 手動執行的 SQL Migration
├── liff-app/                      # Vue 3 前端（LINE LIFF）
├── docker-compose.yml             # 本機開發（API + PostgreSQL + Redis）
├── docker-compose.test.yml        # E2E 測試環境
└── Dockerfile                     # API 容器映像
```

---

## 授權

MIT
