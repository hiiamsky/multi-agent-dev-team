using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using VeggieAlly.Application;
using VeggieAlly.Infrastructure;
using VeggieAlly.WebAPI.Configuration;
using VeggieAlly.WebAPI.Filters;

var builder = WebApplication.CreateBuilder(args);

// ── DI 註冊（按層序） ──
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── WebAPI 設定 ──
builder.Services.Configure<LineSettings>(
    builder.Configuration.GetSection(LineSettings.SectionName));

// ── Filter 註冊 ──
builder.Services.AddScoped<LiffAuthFilter>();
builder.Services.AddScoped<LineSignatureAuthFilter>();

// ── ASP.NET Core ──
builder.Services.AddControllers();

// ── 認證 / 授權（Option A：NullHandler + FallbackPolicy）──
// NullAuthenticationHandler：不實際驗證任何身份，僅讓 UseAuthentication() middleware 正常運行。
// 現有 Controller 標記 [AllowAnonymous]，實際驗證由 LiffAuthFilter / LineSignatureAuthFilter 負責。
// 未來新增的 Controller 若未標記 [AllowAnonymous]，FallbackPolicy 將攔截並返回 401。
const string NullScheme = "Null";
builder.Services
    .AddAuthentication(NullScheme)
    .AddScheme<AuthenticationSchemeOptions, NullAuthenticationHandler>(NullScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── OpenAPI/Swagger 設定 ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "VeggieAlly API", 
        Version = "v1",
        Description = "蔬菜小幫手 - 菜商管理系統 API"
    });
    
    // TODO: 需添加認證設定 (Bearer Token 和 LINE Signature)
    // 由於當前版本 API 變更，暫時標記 TODO
});

var app = builder.Build();

// ── Middleware 管線 ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VeggieAlly API v1");
        c.RoutePrefix = "docs";  // Swagger UI 位於 /docs
    });
}
else
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
