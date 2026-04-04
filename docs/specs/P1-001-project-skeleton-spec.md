# P1-001：.NET 10 Clean Architecture 專案骨架規格

## 文件資訊

| 項目 | 值 |
|------|---|
| Issue | #1 |
| Phase | 1 |
| 上游輸入 | `docs/菜商神隊友_PRD_v2_開發技術版.md` |
| 下游消費者 | 後端 PG |
| 狀態 | Draft |

---

## 1. 系統邊界定義（Phase 1 範圍）

### 1.1 處理流程

```
LINE 使用者
  │ 傳送文字訊息
  ▼
LINE Platform
  │ POST /api/webhook (含 X-Line-Signature)
  ▼
WebhookController
  │ 驗證 HMAC-SHA256 簽章
  │ 反序列化 LineWebhookPayload
  │ 過濾出 type=message & message.type=text
  ▼
MediatR Dispatch
  │ ProcessTextMessageCommand
  ▼
ProcessTextMessageHandler
  │ 組裝 ChatMessage[]（SystemPrompt + 使用者文字）
  │ 呼叫 IChatClient.CompleteAsync()（Gemini API）
  │ 解析回傳 JSON
  ▼
ILineReplyService
  │ POST https://api.line.me/v2/bot/message/reply
  │ 將結構化 JSON 以文字訊息回傳
  ▼
LINE 使用者收到解析結果
```

### 1.2 明確排除項（不在此 Issue 範圍）

| 項目 | 歸屬 |
|------|------|
| 語音 / 圖片處理 | Phase 2 |
| Semantic Kernel Plugins | Phase 2 |
| LINE Flex Message 卡片 | Phase 2 |
| Google Sheets 暫存 | Phase 1 後續 Issue |
| PostgreSQL | Phase 4 |
| Redis | Phase 4 |
| 前端 LIFF / Vue 3 | Phase 3 |
| 多租戶隔離 | Phase 4 |

### 1.3 各層職責摘要

| 層 | Phase 1 職責 |
|----|-------------|
| 前端 | 無 |
| 後端 | 接收 LINE Webhook、驗證簽章、呼叫 Gemini 解析文字、LINE Reply |
| 資料層 | 無（Phase 1 不需任何持久化） |

---

## 2. 架構決策紀錄

### ADR-001: Phase 1 是否建立四層 Clean Architecture？

**決策**：是。四層全部建立。

**理由**：
1. 每一層在 Phase 1 都有**具體內容**，沒有空殼：
   - Domain → LINE 事件模型、解析結果模型、服務介面契約
   - Application → MediatR Command + Handler、System Prompt 定義
   - Infrastructure → LINE Reply 客戶端、MEAI/Gemini 配置、簽章驗證
   - WebAPI → Controller、Filter、DI 組裝
2. 建立成本 < 5 分鐘（4 條 `dotnet new` 指令）
3. 從第一天建立正確依賴方向，避免 Phase 2 拆層重構

### ADR-002: Phase 1 是否需要 MediatR？

**決策**：是。

**理由**：雖然 Phase 1 僅一個 Handler，但 MediatR Pipeline Behavior 可在 Phase 2 無痛加入日誌、驗證等橫切關注。引入成本趨近零（一個 NuGet + 一行 DI 註冊）。

### ADR-003: Phase 1 是否需要 Semantic Kernel？

**決策**：否。Phase 1 僅使用 MEAI `IChatClient` 直接呼叫 Gemini。

**理由**：Phase 1 無 Plugin 需求，SK 在 Phase 2 引入 `VegetableDatabasePlugin` 時才加入。提前引入是無效依賴。

### ADR-004: LINE SDK 選型

**決策**：不使用第三方 LINE SDK，以 `IHttpClientFactory` + Typed Client 薄封裝。

**理由**：Phase 1 僅呼叫 Reply API 一個端點。引入完整 SDK（如 `Line.Messaging`）的依賴面積遠超實際需求。

---

## 3. 專案結構

### 3.1 完整 Tree

```
VeggieAlly/
├── src/
│   ├── VeggieAlly.Domain/
│   │   ├── Models/
│   │   │   ├── Line/
│   │   │   │   ├── LineWebhookPayload.cs
│   │   │   │   ├── LineEvent.cs
│   │   │   │   ├── LineEventSource.cs
│   │   │   │   └── LineMessage.cs
│   │   │   └── Parsing/
│   │   │       ├── ParsedMenuItem.cs
│   │   │       └── ParseResult.cs
│   │   ├── Abstractions/
│   │   │   └── ILineReplyService.cs
│   │   └── VeggieAlly.Domain.csproj
│   │
│   ├── VeggieAlly.Application/
│   │   ├── LineEvents/
│   │   │   └── ProcessText/
│   │   │       ├── ProcessTextMessageCommand.cs
│   │   │       └── ProcessTextMessageHandler.cs
│   │   ├── Prompts/
│   │   │   └── SystemPrompts.cs
│   │   ├── DependencyInjection.cs
│   │   └── VeggieAlly.Application.csproj
│   │
│   ├── VeggieAlly.Infrastructure/
│   │   ├── Line/
│   │   │   ├── LineReplyService.cs
│   │   │   └── LineSignatureValidator.cs
│   │   ├── AI/
│   │   │   └── GeminiChatClientFactory.cs
│   │   ├── DependencyInjection.cs
│   │   └── VeggieAlly.Infrastructure.csproj
│   │
│   └── VeggieAlly.WebAPI/
│       ├── Controllers/
│       │   └── WebhookController.cs
│       ├── Filters/
│       │   └── LineSignatureAuthFilter.cs
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Program.cs
│       └── VeggieAlly.WebAPI.csproj
│
├── tests/
│   └── VeggieAlly.Application.Tests/
│       └── VeggieAlly.Application.Tests.csproj
│
├── VeggieAlly.sln
├── Directory.Build.props
├── .gitignore
├── Dockerfile
└── docker-compose.yml
```

### 3.2 專案引用關係（依賴方向：外層 → 內層）

```
WebAPI ──→ Application ──→ Domain
  │              ↑
  └──→ Infrastructure ──┘
```

| 專案 | 引用 |
|------|------|
| `VeggieAlly.Domain` | 無 |
| `VeggieAlly.Application` | `VeggieAlly.Domain` |
| `VeggieAlly.Infrastructure` | `VeggieAlly.Application` |
| `VeggieAlly.WebAPI` | `VeggieAlly.Application`、`VeggieAlly.Infrastructure` |

### 3.3 NuGet 套件

| 專案 | 套件 | 用途 |
|------|------|------|
| Domain | （無） | 純 C# 模型與介面，零外部依賴 |
| Application | `MediatR` >= 12.0 | `IRequest`、`IRequestHandler` |
| Application | `Microsoft.Extensions.AI.Abstractions` | `IChatClient`、`ChatMessage` 介面 |
| Infrastructure | `Microsoft.Extensions.AI` | `ChatClientBuilder`、`AddChatClient()` 擴充 |
| Infrastructure | **Gemini MEAI Adapter**（見備註） | `IChatClient` 的 Gemini 實作 |
| Infrastructure | `Microsoft.Extensions.Http` | `IHttpClientFactory` |
| Infrastructure | `Microsoft.Extensions.Options` | `IOptions<T>` 設定繫結 |
| WebAPI | （隨 ASP.NET Core 10 SDK 內建） | Controller、OpenAPI |
| Tests | `xunit` | 測試框架 |
| Tests | `NSubstitute` | Mock |

> **備註：Gemini MEAI Adapter 套件**
> 候選套件：`Microsoft.Extensions.AI.Google` 或 `Mscc.GenerativeAI`（社群維護）。
> 後端 PG 於實作時執行 `dotnet nuget search "Gemini" --source nuget.org` 確認最新可用套件名稱與版本。
> 選型原則：必須實作 `IChatClient` 介面，優先選 Microsoft 官方或 Google 官方發行。

---

## 4. 各層職責與命名慣例

### 4.1 Domain 層

| 項目 | 規格 |
|------|------|
| 職責 | 定義核心模型（Value Object、DTO）與服務介面契約 |
| 依賴 | 零外部依賴 |
| 命名空間 | `VeggieAlly.Domain.Models.{子分類}`、`VeggieAlly.Domain.Abstractions` |

**Phase 1 具體內容**：

`Models/Line/LineWebhookPayload.cs`：
```csharp
namespace VeggieAlly.Domain.Models.Line;

public sealed record LineWebhookPayload(List<LineEvent> Events);
```

`Models/Line/LineEvent.cs`：
```csharp
namespace VeggieAlly.Domain.Models.Line;

public sealed record LineEvent(
    string Type,
    string? ReplyToken,
    LineEventSource Source,
    LineMessage? Message);
```

`Models/Line/LineEventSource.cs`：
```csharp
namespace VeggieAlly.Domain.Models.Line;

public sealed record LineEventSource(string Type, string UserId);
```

`Models/Line/LineMessage.cs`：
```csharp
namespace VeggieAlly.Domain.Models.Line;

public sealed record LineMessage(string Id, string Type, string? Text);
```

`Models/Parsing/ParsedMenuItem.cs`：
```csharp
namespace VeggieAlly.Domain.Models.Parsing;

public sealed record ParsedMenuItem(
    string Name,
    bool IsNew,
    decimal BuyPrice,
    decimal SellPrice,
    int Quantity,
    string Unit);
```

`Models/Parsing/ParseResult.cs`：
```csharp
namespace VeggieAlly.Domain.Models.Parsing;

public sealed record ParseResult(List<ParsedMenuItem> Items);
```

`Abstractions/ILineReplyService.cs`：
```csharp
namespace VeggieAlly.Domain.Abstractions;

public interface ILineReplyService
{
    Task ReplyTextAsync(string replyToken, string text, CancellationToken ct = default);
}
```

### 4.2 Application 層

| 項目 | 規格 |
|------|------|
| 職責 | CQRS Command/Handler、System Prompt 定義、DI 註冊擴充 |
| 依賴 | Domain、MediatR、MEAI Abstractions |
| 命名空間 | `VeggieAlly.Application.{Feature}.{Operation}` |
| 命名慣例 | Command = `{動詞}{名詞}Command`，Handler = `{動詞}{名詞}Handler` |

**CQRS 資料夾慣例**：
```
Application/
  └── LineEvents/              ← Feature 名稱
      └── ProcessText/         ← Operation 名稱
          ├── ProcessTextMessageCommand.cs
          └── ProcessTextMessageHandler.cs
```

`LineEvents/ProcessText/ProcessTextMessageCommand.cs`：
```csharp
namespace VeggieAlly.Application.LineEvents.ProcessText;

public sealed record ProcessTextMessageCommand(LineEvent Event) : IRequest;
```

`LineEvents/ProcessText/ProcessTextMessageHandler.cs` 職責規格：
1. 從 `Command.Event.Message.Text` 取得使用者輸入文字
2. 組裝 `ChatMessage[]`：SystemPrompt（蔬菜解析指令）+ 使用者文字
3. 呼叫 `IChatClient.CompleteAsync()`
4. 從回傳內容擷取 JSON 字串
5. 呼叫 `ILineReplyService.ReplyTextAsync()` 將 JSON 回傳給使用者
6. **內部 try-catch**：Gemini 呼叫失敗時，改回傳錯誤訊息「解析失敗，請重新輸入」，**不拋出例外**

`Prompts/SystemPrompts.cs`：
```csharp
namespace VeggieAlly.Application.Prompts;

public static class SystemPrompts
{
    public const string VegetableParser = """
        你是蔬菜批發報價解析助手。
        使用者會輸入今日進貨的品項與價格資訊。
        請將內容解析為以下 JSON 格式，不要輸出任何其他內容：
        {
          "items": [
            {
              "name": "品項名稱",
              "is_new": false,
              "buy_price": 0,
              "sell_price": 0,
              "quantity": 0,
              "unit": "箱"
            }
          ]
        }
        規則：
        1. name 須對應以下標準品項清單，若無法對應則 is_new 設為 true
        2. 金額單位為新台幣，數量預設單位為「箱」
        3. 若使用者未提供 sell_price，該欄位設為 0
        品項清單：高麗菜、包心大白菜、小白菜、青江菜、空心菜、油菜、莧菜、菠菜、A菜、萵苣、白蘿蔔、紅蘿蔔、馬鈴薯、洋蔥、地瓜、芋頭、蔥、蒜頭、薑、牛番茄、聖女番茄、小黃瓜、胡瓜、絲瓜、苦瓜、茄子、彩椒、青椒、敏豆、四季豆、青花菜、花椰菜、玉米、玉米筍、豌豆、秋葵、南瓜、冬瓜、蘆筍、九層塔、金針菇、杏鮑菇、香菇、秀珍菇、鴻喜菇、黑木耳、白木耳、香菜、芹菜、辣椒
        """;
}
```

### 4.3 Infrastructure 層

| 項目 | 規格 |
|------|------|
| 職責 | 外部服務整合（LINE API、Gemini API）、介面實作、設定選項類別 |
| 依賴 | Application（間接取得 Domain） |
| 命名空間 | `VeggieAlly.Infrastructure.{外部服務名}` |

**Phase 1 具體內容**：

`Line/LineReplyService.cs`：
- 實作 `ILineReplyService`
- 透過 `HttpClient`（由 `IHttpClientFactory` 管理）呼叫 `POST https://api.line.me/v2/bot/message/reply`
- Request Header：`Authorization: Bearer {ChannelAccessToken}`
- Request Body：`{ "replyToken": "...", "messages": [{ "type": "text", "text": "..." }] }`
- 訊息長度上限：5000 字元（LINE API 限制），超出時截斷

`Line/LineSignatureValidator.cs`：
- 靜態方法 `bool Validate(string channelSecret, byte[] requestBody, string signature)`
- 演算法：HMAC-SHA256（key = channelSecret UTF-8 bytes，data = requestBody）
- 產出 Base64 字串，與 `signature` 做**常數時間比對**（`CryptographicOperations.FixedTimeEquals`）

`AI/GeminiChatClientFactory.cs`：
- 負責建構 `IChatClient` 實例的工廠邏輯（若 Adapter 套件不直接提供 DI 擴充）
- 讀取 `GeminiOptions` 組態

### 4.4 WebAPI 層

| 項目 | 規格 |
|------|------|
| 職責 | HTTP 進入點、DI Composition Root、中介軟體管線、Action Filter |
| 依賴 | Application、Infrastructure |
| 命名空間 | `VeggieAlly.WebAPI.{分類}` |

`Controllers/WebhookController.cs` 規格：
- Route：`[Route("api/[controller]")]`
- 單一 Action：`[HttpPost] Receive([FromBody] LineWebhookPayload payload)`
- 套用 `[TypeFilter(typeof(LineSignatureAuthFilter))]`
- 過濾條件：僅處理 `evt.Type == "message"` 且 `evt.Message.Type == "text"` 的事件
- **永遠回傳 `200 OK`**：避免 LINE Platform 重試投遞。Handler 內部自行處理錯誤

`Filters/LineSignatureAuthFilter.cs`：
- 實作 `IAsyncActionFilter`
- 透過 DI 注入 `IOptions<LineOptions>` 取得 `ChannelSecret`
- 流程：`Request.EnableBuffering()` → 讀取 Body → 呼叫 `LineSignatureValidator.Validate()` → 失敗回傳 `401`，成功呼叫 `next()`
- 讀取完畢後必須 `Request.Body.Position = 0` 歸位

---

## 5. 時序邏輯

```
LINE Platform          WebhookController       LineSignatureAuthFilter       MediatR        ProcessTextMessageHandler      IChatClient(Gemini)      ILineReplyService
     │                       │                        │                       │                    │                          │                       │
     │──POST /api/webhook───▶│                        │                       │                    │                          │                       │
     │                       │──OnActionExecutionAsync▶│                       │                    │                          │                       │
     │                       │                        │─Validate Signature─┐  │                    │                          │                       │
     │                       │                        │◀──────────────────┘   │                    │                          │                       │
     │                       │                        │  [pass]               │                    │                          │                       │
     │                       │◀───────next()──────────│                       │                    │                          │                       │
     │                       │                        │                       │                    │                          │                       │
     │                       │──Send(Command)─────────────────────────────────▶│                   │                          │                       │
     │                       │                        │                       │                    │                          │                       │
     │                       │                        │                       │                    │──CompleteAsync(messages)─▶│                       │
     │                       │                        │                       │                    │◀──parsed JSON────────────│                       │
     │                       │                        │                       │                    │                          │                       │
     │                       │                        │                       │                    │──ReplyTextAsync()────────────────────────────────▶│
     │                       │                        │                       │                    │◀─────────────────────────────────────────────────│
     │                       │                        │                       │                    │                          │                       │
     │◀──200 OK──────────────│                        │                       │                    │                          │                       │
```

---

## 6. API 規格

### `[POST] /api/webhook`

**用途**：接收 LINE Platform Webhook 推送

**Request Header**：

| Header | 必要 | 說明 |
|--------|------|------|
| `Content-Type` | 是 | `application/json` |
| `X-Line-Signature` | 是 | HMAC-SHA256(body, ChannelSecret) 的 Base64 編碼 |

**Request Body**：
```json
{
  "events": [
    {
      "type": "message",
      "replyToken": "nHuyWiB7yP5Zw52FIkcQobQuGDXCTA",
      "source": {
        "type": "user",
        "userId": "U206d25c2ea6bd87c17655609a1c37cb8"
      },
      "message": {
        "id": "325708",
        "type": "text",
        "text": "高麗菜 25 50箱 小白菜 15 30箱"
      }
    }
  ]
}
```

**Response**：

| 狀態碼 | 條件 | Body |
|--------|------|------|
| `200 OK` | 簽章驗證通過（不論後續處理成敗） | 空 |
| `401 Unauthorized` | `X-Line-Signature` 缺失或驗證失敗 | 空 |

> **關鍵設計**：Controller 永遠回傳 `200`。Gemini 呼叫或 LINE Reply 失敗時，Handler 內部 catch 並記錄日誌，嘗試回傳使用者友善錯誤訊息。不向 LINE Platform 回傳非 200 狀態碼。

---

## 7. DI 註冊規格

### 7.1 各層擴充方法慣例

每個非 WebAPI 層提供一個靜態擴充類別 `DependencyInjection.cs`，暴露 `Add{層名}()` 方法。

**Application/DependencyInjection.cs**：
```csharp
namespace VeggieAlly.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
```

**Infrastructure/DependencyInjection.cs**：
```csharp
namespace VeggieAlly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── LINE ──
        services.Configure<LineOptions>(configuration.GetSection("Line"));
        services.AddHttpClient<ILineReplyService, LineReplyService>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://api.line.me");
        });

        // ── AI: Gemini via MEAI ──
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
        services.AddChatClient(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            // 依實際 Adapter 套件調整建構方式
            return GeminiChatClientFactory.Create(opts.ApiKey, opts.ModelId);
        });

        return services;
    }
}
```

### 7.2 Options 類別

```csharp
namespace VeggieAlly.Infrastructure.Line;

public sealed class LineOptions
{
    public required string ChannelSecret { get; init; }
    public required string ChannelAccessToken { get; init; }
}
```

```csharp
namespace VeggieAlly.Infrastructure.AI;

public sealed class GeminiOptions
{
    public required string ApiKey { get; init; }
    public string ModelId { get; init; } = "gemini-2.0-flash";
}
```

### 7.3 WebAPI Program.cs 組裝順序

```csharp
var builder = WebApplication.CreateBuilder(args);

// ── DI 註冊（按層序） ──
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── ASP.NET Core ──
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middleware 管線 ──
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
```

---

## 8. Configuration 結構

### 8.1 appsettings.json（提交至版控，不含機密）

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Line": {
    "ChannelSecret": "",
    "ChannelAccessToken": ""
  },
  "Gemini": {
    "ApiKey": "",
    "ModelId": "gemini-2.0-flash"
  }
}
```

### 8.2 開發環境：User Secrets

```bash
cd src/VeggieAlly.WebAPI
dotnet user-secrets init
dotnet user-secrets set "Line:ChannelSecret" "<your-channel-secret>"
dotnet user-secrets set "Line:ChannelAccessToken" "<your-channel-access-token>"
dotnet user-secrets set "Gemini:ApiKey" "<your-gemini-api-key>"
```

### 8.3 生產環境：環境變數

| 環境變數名稱 | 對應設定 |
|-------------|---------|
| `Line__ChannelSecret` | `Line:ChannelSecret` |
| `Line__ChannelAccessToken` | `Line:ChannelAccessToken` |
| `Gemini__ApiKey` | `Gemini:ApiKey` |
| `Gemini__ModelId` | `Gemini:ModelId`（選填，有預設值） |

### 8.4 .gitignore 必要條目

```gitignore
**/appsettings.Development.json
```

> `appsettings.Development.json` **不提交**至版控，僅供本機覆寫。機密一律走 User Secrets 或環境變數。

---

## 9. 例外處理與邊界條件

| 場景 | 處理方式 |
|------|---------|
| `X-Line-Signature` 缺失或比對失敗 | `LineSignatureAuthFilter` 回傳 `401 Unauthorized`，不進入 Handler |
| `events` 陣列為空 | Controller 直接回傳 `200 OK`，不分派任何 Command |
| Event 非 `message` 類型或非 `text` 類型 | Controller 跳過該事件，Phase 1 僅處理文字 |
| `replyToken` 為 `null`（Webhook 驗證事件） | Controller 跳過該事件 |
| Gemini API 回傳非預期格式（非 JSON） | Handler catch → `ILineReplyService` 回覆「解析失敗，請重新輸入」 |
| Gemini API 逾時或 HTTP 錯誤 | Handler catch → `ILineReplyService` 回覆「系統忙碌中，請稍後重試」→ 記錄 `ILogger.LogError` |
| LINE Reply API 失敗 | Handler catch → 僅記錄 `ILogger.LogWarning`，不再嘗試（無法回覆） |
| Request Body 反序列化失敗 | ASP.NET Core 內建回傳 `400 Bad Request`（此時 Filter 尚未執行，不影響安全性） |

---

## 10. 專案建立步驟（dotnet CLI）

以下指令由後端 PG 在 repository root 下依序執行：

```bash
# 0. 確認 .NET 10 SDK
dotnet --version  # 應為 10.x.xxx

# 1. 建立 Solution
dotnet new sln -n VeggieAlly -o VeggieAlly
cd VeggieAlly

# 2. 建立 Directory.Build.props
cat > Directory.Build.props << 'EOF'
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
EOF

# 3. 建立各層專案
mkdir -p src tests

dotnet new classlib -n VeggieAlly.Domain -o src/VeggieAlly.Domain
dotnet new classlib -n VeggieAlly.Application -o src/VeggieAlly.Application
dotnet new classlib -n VeggieAlly.Infrastructure -o src/VeggieAlly.Infrastructure
dotnet new webapi -n VeggieAlly.WebAPI -o src/VeggieAlly.WebAPI --use-controllers
dotnet new xunit -n VeggieAlly.Application.Tests -o tests/VeggieAlly.Application.Tests

# 4. 加入 Solution
dotnet sln add src/VeggieAlly.Domain
dotnet sln add src/VeggieAlly.Application
dotnet sln add src/VeggieAlly.Infrastructure
dotnet sln add src/VeggieAlly.WebAPI
dotnet sln add tests/VeggieAlly.Application.Tests

# 5. 設定專案引用
dotnet add src/VeggieAlly.Application reference src/VeggieAlly.Domain
dotnet add src/VeggieAlly.Infrastructure reference src/VeggieAlly.Application
dotnet add src/VeggieAlly.WebAPI reference src/VeggieAlly.Application
dotnet add src/VeggieAlly.WebAPI reference src/VeggieAlly.Infrastructure
dotnet add tests/VeggieAlly.Application.Tests reference src/VeggieAlly.Application

# 6. 安裝 NuGet 套件
dotnet add src/VeggieAlly.Application package MediatR
dotnet add src/VeggieAlly.Application package Microsoft.Extensions.AI.Abstractions

dotnet add src/VeggieAlly.Infrastructure package Microsoft.Extensions.AI
dotnet add src/VeggieAlly.Infrastructure package Microsoft.Extensions.Http
dotnet add src/VeggieAlly.Infrastructure package Microsoft.Extensions.Options.ConfigurationExtensions
# ⚠️ Gemini Adapter 套件需確認後補：
# dotnet add src/VeggieAlly.Infrastructure package <GeminiAdapterPackage>

dotnet add tests/VeggieAlly.Application.Tests package NSubstitute

# 7. 移除各 classlib 預設產生的 Class1.cs
rm src/VeggieAlly.Domain/Class1.cs
rm src/VeggieAlly.Application/Class1.cs
rm src/VeggieAlly.Infrastructure/Class1.cs

# 8. 初始化 User Secrets
dotnet user-secrets init --project src/VeggieAlly.WebAPI

# 9. 驗證建置
dotnet build
```

> **注意**：各 classlib 的 `.csproj` 中 `<TargetFramework>` 會由 `Directory.Build.props` 統一覆寫，無需個別修改。但須手動移除 classlib 模板自動產生的 `<TargetFramework>` 行，避免衝突。

---

## 11. 驗收 Checklist

後端 PG 完成實作後，須逐項確認以下條件。全部通過方可提交 QA/QC。

| # | 驗收項目 | 驗證方式 |
|---|---------|---------|
| 1 | `dotnet build` 零錯誤、零警告 | CLI 執行 |
| 2 | 專案引用方向正確：Domain 無任何 ProjectReference | 檢查 `.csproj` |
| 3 | Domain 層零 NuGet 外部依賴 | 檢查 `.csproj` |
| 4 | `appsettings.json` 不含任何實際密鑰值 | 人工檢查 |
| 5 | `appsettings.Development.json` 已列入 `.gitignore` | `git status` 確認未追蹤 |
| 6 | User Secrets 設定完成後，`dotnet run` 可正常啟動 | CLI 執行 |
| 7 | 發送假 Webhook（無效簽章）→ 回傳 `401` | `curl` 或 Postman |
| 8 | 發送有效簽章 + text message Webhook → 回傳 `200`，且 LINE 使用者收到 Gemini 解析的 JSON 回應 | ngrok + LINE 實測 |
| 9 | 發送有效簽章 + 非 text 事件（如 follow）→ 回傳 `200`，無副作用 | `curl` |
| 10 | Gemini API Key 故意填錯 → 使用者收到「系統忙碌中，請稍後重試」| LINE 實測 |
| 11 | `dotnet test` 全部通過 | CLI 執行 |
| 12 | Dockerfile `docker build` 成功 | CLI 執行 |

---

*此藍圖為後端 PG 的唯一實作依據。任何規格不清之處，退回 SA/SD 澄清，不得自行推測。*
