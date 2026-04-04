using Microsoft.Extensions.AI;

namespace VeggieAlly.Infrastructure.AI;

internal sealed class GeminiChatClientAdapter : IChatClient
{
    private readonly string _apiKey;
    private readonly string _modelId;

    public GeminiChatClientAdapter(string apiKey, string modelId)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
    }

    public ChatClientMetadata Metadata => new("Gemini");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages, 
        ChatOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        if (chatMessages == null || !chatMessages.Any())
        {
            throw new ArgumentException("ChatMessages 不可為空", nameof(chatMessages));
        }

        try
        {
            // 將 ChatMessage 轉換為 Gemini 格式
            var prompt = ConvertMessagesToPrompt(chatMessages);

            // TODO: 實際呼叫 Gemini API（需要澄清 Mscc.GenerativeAI 套件使用方式）
            // 暫時回傳固定的 JSON 格式
            var responseText = """
                {
                  "items": [
                    {
                      "name": "範例品項",
                      "is_new": false,
                      "buy_price": 100,
                      "sell_price": 120,
                      "quantity": 1,
                      "unit": "箱"
                    }
                  ]
                }
                """;

            // 將回傳結果包裝成 ChatResponse
            var responseMessage = new ChatMessage(ChatRole.Assistant, responseText);
            return new ChatResponse(responseMessage);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Gemini API 呼叫失敗: {ex.Message}", ex);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages, 
        ChatOptions? options = null, 
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("串流模式暫不支援");
    }

    public TService? GetService<TService>(object? key = null) where TService : class
    {
        return null; // 不提供額外服務
    }

    public object? GetService(Type serviceType, object? key = null)
    {
        return null; // 不提供額外服務
    }

    private static string ConvertMessagesToPrompt(IEnumerable<ChatMessage> messages)
    {
        // 簡化實作：將所有訊息串接成單一 prompt
        var messageList = messages.ToList();
        var systemMessages = messageList.Where(m => m.Role == ChatRole.System).Select(m => m.Text);
        var userMessages = messageList.Where(m => m.Role == ChatRole.User).Select(m => m.Text);
        var assistantMessages = messageList.Where(m => m.Role == ChatRole.Assistant).Select(m => m.Text);

        var prompt = string.Empty;
        
        if (systemMessages.Any())
        {
            prompt += string.Join("\n", systemMessages) + "\n\n";
        }
        
        // 交替組合 user 和 assistant 訊息（簡化實作）
        var allMessages = messageList.Where(m => m.Role == ChatRole.User || m.Role == ChatRole.Assistant);
        foreach (var message in allMessages)
        {
            if (message.Role == ChatRole.User)
            {
                prompt += $"Human: {message.Text}\n";
            }
            else if (message.Role == ChatRole.Assistant)
            {
                prompt += $"Assistant: {message.Text}\n";
            }
        }

        // 如果只有 system + user，則直接組合
        if (!assistantMessages.Any() && userMessages.Count() == 1)
        {
            var userMessage = userMessages.First();
            return systemMessages.Any() ? $"{string.Join("\n", systemMessages)}\n\n{userMessage}" : userMessage;
        }

        return prompt.Trim();
    }

    public void Dispose()
    {
        // 無需釋放資源
    }
}