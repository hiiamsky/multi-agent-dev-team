# P2-003 語音訊息 (Audio Event) 處理與 STT 解析 — SA/SD 規格藍圖

> **Issue**: #12  
> **狀態**: Draft  
> **前置**: P2-001 (價格驗證), P2-002 (Flex Message)

---

## 1. 目標

使用者透過 LINE 傳送語音訊息給 Bot，Bot 透過 LINE Content API 下載 .m4a 音檔，
經由 MEAI `IChatClient`（Gemini 多模態）一次完成 **STT + 結構化 JSON 解析**，
解析結果進入 P2-001 驗證流程並以 P2-002 Flex Message 卡片回覆。

---

## 2. 架構設計

### 2.1 資料流

```
LINE 使用者傳語音
  → Webhook (type=message, message.type=audio)
  → WebhookController 路由 → ProcessAudioMessageCommand
  → ProcessAudioMessageHandler
       1. ILineContentService.DownloadContentAsync(messageId) → byte[]
       2. 建立 ChatMessage: System=VegetableParser, User=DataContent(audio/m4a)
       3. IChatClient.GetResponseAsync → JSON string
       4. IValidationReplyService.ValidateAndReplyAsync(json, replyToken)
            → JSON 反序列化 → 價格查詢 → 驗證 → Flex Message → LINE Reply
```

### 2.2 新增元件

| 層級 | 元件 | 職責 |
|------|------|------|
| Domain | `ILineContentService` | LINE Content API 抽象 |
| Application | `IValidationReplyService` | 從 JSON 到 Flex 回覆的共用管線 |
| Application | `ValidationReplyService` | 實作：反序列化 → 驗價 → Flex → Reply |
| Application | `ProcessAudioMessageCommand` | MediatR Command |
| Application | `ProcessAudioMessageHandler` | 音訊下載 → LLM → 驗證回覆 |
| Infrastructure | `LineContentService` | HTTP 呼叫 LINE Content API |

### 2.3 既有元件修改

| 元件 | 修改 |
|------|------|
| `WebhookController` | 增加 `message.type == "audio"` 路由 |
| `ProcessTextMessageHandler` | 抽出共用驗證管線至 `IValidationReplyService` |
| `DependencyInjection` | 註冊 `ILineContentService`, `IValidationReplyService` |

---

## 3. 介面規格

### 3.1 ILineContentService

```csharp
namespace VeggieAlly.Domain.Abstractions;

public interface ILineContentService
{
    Task<byte[]> DownloadContentAsync(string messageId, CancellationToken ct = default);
}
```

### 3.2 LineContentService

```csharp
// GET https://api-data.line.me/v2/bot/message/{messageId}/content
// Authorization: Bearer {ChannelAccessToken}
// Response: binary audio data
```

### 3.3 IValidationReplyService

```csharp
namespace VeggieAlly.Application.Common.Interfaces;

public interface IValidationReplyService
{
    Task ValidateAndReplyAsync(string jsonContent, string replyToken, CancellationToken ct = default);
}
```

### 3.4 ProcessAudioMessageCommand

```csharp
public sealed record ProcessAudioMessageCommand(LineEvent Event) : IRequest;
```

---

## 4. MEAI 多模態呼叫規格

```csharp
// audio bytes from LINE Content API
var audioContent = new DataContent(audioBytes, "audio/m4a");
var messages = new ChatMessage[]
{
    new(ChatRole.System, SystemPrompts.VegetableParser),
    new(ChatRole.User, [audioContent])
};
var response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct);
```

重點：Gemini 多模態 API 在同一次呼叫中完成 STT + 結構化解析，不需兩次 LLM 呼叫。

---

## 5. WebhookController 路由更新

```
foreach (var lineEvent in payload.Events)
{
    if (lineEvent.Type != "message") continue;
    if (string.IsNullOrWhiteSpace(lineEvent.ReplyToken)) continue;

    switch (lineEvent.Message?.Type)
    {
        case "text":
            await _mediator.Send(new ProcessTextMessageCommand(lineEvent));
            break;
        case "audio":
            await _mediator.Send(new ProcessAudioMessageCommand(lineEvent));
            break;
        default:
            _logger.LogDebug("跳過不支援的訊息類型: {Type}", lineEvent.Message?.Type);
            break;
    }
}
```

---

## 6. 錯誤處理策略

| 場景 | 處理 |
|------|------|
| LINE Content API 下載失敗 | Reply "語音下載失敗，請重新錄音" |
| 音檔為空 (0 bytes) | Reply "語音內容為空，請重新錄音" |
| Gemini STT 解析失敗 (非 JSON) | Reply "語音解析失敗，請重新輸入" |
| Gemini API 例外 | Reply "系統忙碌中，請稍後重試" |
| 驗證管線失敗 | 同 ProcessTextMessageHandler 既有邏輯 |

---

## 7. 測試矩陣

### 7.1 ProcessAudioMessageHandlerTests

| # | 測試案例 | 預期 |
|---|---------|------|
| 1 | 正常音訊 → JSON → 驗證 OK | `ValidateAndReplyAsync` called |
| 2 | MessageId 為空 | 不下載，Reply 錯誤訊息 |
| 3 | ReplyToken 為空 | 不處理 |
| 4 | Content 下載失敗 (exception) | Reply "語音下載失敗" |
| 5 | Content 回傳空陣列 | Reply "語音內容為空" |
| 6 | LLM 回傳非 JSON | `ValidateAndReplyAsync` 內處理 |
| 7 | LLM 例外 | Reply "系統忙碌中" |

### 7.2 ValidationReplyServiceTests

| # | 測試案例 | 預期 |
|---|---------|------|
| 1 | 有效 JSON + 驗證 OK | ReplyFlexAsync called |
| 2 | 有效 JSON + 異常 | ReplyFlexAsync with anomaly altText |
| 3 | 無效 JSON | ReplyTextAsync "解析失敗" |
| 4 | 空 items | ReplyTextAsync "解析失敗" |
| 5 | Flex 建構失敗 | 降級純文字 |

### 7.3 WebhookControllerTests (新增)

| # | 測試案例 | 預期 |
|---|---------|------|
| 1 | Audio message event | Dispatches `ProcessAudioMessageCommand` |
| 2 | Audio without ReplyToken | 跳過 |

### 7.4 E2E 測試向量

| # | 向量 | 預期 |
|---|------|------|
| E1 | HTTP POST audio event payload | HTTP 200 + handler 觸發 |

---

## 8. 檔案清單

### 新增
- `Domain/Abstractions/ILineContentService.cs`
- `Application/Common/Interfaces/IValidationReplyService.cs`
- `Application/Services/ValidationReplyService.cs`
- `Application/LineEvents/ProcessAudio/ProcessAudioMessageCommand.cs`
- `Application/LineEvents/ProcessAudio/ProcessAudioMessageHandler.cs`
- `Infrastructure/Line/LineContentService.cs`
- `tests/.../ProcessAudioMessageHandlerTests.cs`
- `tests/.../ValidationReplyServiceTests.cs`

### 修改
- `WebAPI/Controllers/WebhookController.cs`
- `Application/LineEvents/ProcessText/ProcessTextMessageHandler.cs`
- `Infrastructure/DependencyInjection.cs`
- `tests/.../WebhookControllerTests.cs`
- `tests/.../ProcessTextMessageHandlerTests.cs`
