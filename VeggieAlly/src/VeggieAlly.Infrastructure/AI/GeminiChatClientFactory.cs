using Microsoft.Extensions.AI;

namespace VeggieAlly.Infrastructure.AI;

public static class GeminiChatClientFactory
{
    public static IChatClient Create(string apiKey, string modelId)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Gemini API Key 不可為空", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model ID 不可為空", nameof(modelId));
        }

        // 暫時實作：建立 Adapter 來包裝 IChatClient 介面
        // TODO: 整合實際的 Mscc.GenerativeAI 套件
        return new GeminiChatClientAdapter(apiKey, modelId);
    }
}