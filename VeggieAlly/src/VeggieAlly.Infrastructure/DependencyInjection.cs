using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Infrastructure.AI;
using VeggieAlly.Infrastructure.Line;

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
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
            return GeminiChatClientFactory.Create(opts.ApiKey, opts.ModelId);
        });

        return services;
    }
}