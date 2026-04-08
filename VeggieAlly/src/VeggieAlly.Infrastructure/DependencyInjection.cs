using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using Dapper;
using Npgsql;
using OpenAI;
using StackExchange.Redis;
using VeggieAlly.Application.Common.Interfaces;
using VeggieAlly.Application.Services;
using VeggieAlly.Domain.Abstractions;
using VeggieAlly.Infrastructure.AI;
using VeggieAlly.Infrastructure.Cache;
using VeggieAlly.Infrastructure.Line;
using VeggieAlly.Infrastructure.Persistence;
using VeggieAlly.Infrastructure.Services;
using VeggieAlly.Infrastructure.Storage;

namespace VeggieAlly.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Dapper 全域型別處理器 ──
        // DateOnly 在 Dapper 預設不受支援，需要手動註冊 TypeHandler
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        // ── LINE ──
        services.Configure<LineOptions>(configuration.GetSection("Line"));
        services.AddHttpClient<ILineReplyService, LineReplyService>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://api.line.me");
        });
        services.AddHttpClient<ILineContentService, LineContentService>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://api-data.line.me");
        });

        // ── LINE Token Service ──
        services.AddHttpClient<ILineTokenService, LineTokenService>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://api.line.me");
        });

        // ── Tenant Config Service ──
        services.AddScoped<ITenantConfigService, TenantConfigService>();

        // ── LIFF Config Service ──
        services.AddScoped<ILiffConfigService, LiffConfigService>();

        // ── Draft Session Store（Redis / In-Memory 切換）──
        var redisConnectionString = configuration.GetValue<string>("Redis:ConnectionString");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 1;
            redisOptions.ConnectTimeout = 5000;
            redisOptions.AsyncTimeout = 5000;

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
            services.AddScoped<IDraftSessionStore, RedisDraftSessionStore>();
        }
        else
        {
            var inMemoryStore = new InMemoryDraftSessionStore();
            services.AddSingleton<IDraftSessionStore>(inMemoryStore);
            services.AddSingleton(inMemoryStore); // 為 CleanupService 提供具體型別
            services.AddHostedService<InMemoryDraftSessionCleanupService>();
        }

        // ── Draft Menu Service ──
        services.AddScoped<IDraftMenuService, DraftMenuService>();

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

        // ── Flex Message Builder ──
        services.AddSingleton<IFlexMessageBuilder, FlexMessageBuilder>();

        // ── Validation Reply Pipeline ──
        services.AddScoped<IValidationReplyService, ValidationReplyService>();

        // ── PostgreSQL + Published Menu Services ──
        var pgConnectionString = configuration.GetConnectionString("PostgreSQL");
        if (!string.IsNullOrWhiteSpace(pgConnectionString))
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(pgConnectionString);
            var dataSource = dataSourceBuilder.Build();
            services.AddSingleton(dataSource);
            
            // Repository 服務
            services.AddScoped<IPublishedMenuRepository, PublishedMenuRepository>();
            
            // Cache 服務
            if (!string.IsNullOrWhiteSpace(redisConnectionString))
            {
                services.AddScoped<IPublishedMenuCache, PublishedMenuCache>();
            }
            else
            {
                services.AddScoped<IPublishedMenuCache, NoOpPublishedMenuCache>();
            }
        }

        return services;
    }
}