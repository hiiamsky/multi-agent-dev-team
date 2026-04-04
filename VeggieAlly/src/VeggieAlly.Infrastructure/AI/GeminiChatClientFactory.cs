using Microsoft.Extensions.AI;
using Mscc.GenerativeAI.Microsoft;

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

        return new GeminiChatClient(apiKey: apiKey, model: modelId);
    }
}