using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using OpenAI;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Infrastructure.AI;
using VeggieAlly.Infrastructure.Line;
using VeggieAlly.Infrastructure.Services;

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

        // ── AI: 根據 AI:Provider 切換後端 ──
        var aiProvider = configuration.GetValue<string>("AI:Provider") ?? "gemini";

        if (aiProvider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration.GetValue<string>("Ollama:Endpoint") ?? "http://localhost:11434/v1";
            var modelId = configuration.GetValue<string>("Ollama:ModelId") ?? "gemma4:31b";

            services.AddChatClient(sp =>
            {
                var client = new OpenAIClient(
                    new System.ClientModel.ApiKeyCredential("ollama"),
                    new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
                return client.GetChatClient(modelId).AsIChatClient();
            });
        }
        else
        {
            services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
            services.AddChatClient(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
                return GeminiChatClientFactory.Create(opts.ApiKey, opts.ModelId);
            });
        }

        // ── Price Validation Services ──
        services.AddScoped<IVegetablePricingService, MockVegetablePricingService>();
        services.AddScoped<IPriceValidationService, PriceValidationService>();

        return services;
    }
}