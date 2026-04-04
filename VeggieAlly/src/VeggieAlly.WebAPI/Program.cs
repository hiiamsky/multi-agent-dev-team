using VeggieAlly.Application;
using VeggieAlly.Infrastructure;
using VeggieAlly.WebAPI.Filters;

var builder = WebApplication.CreateBuilder(args);

// ── DI 註冊（按層序） ──
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── Filter 註冊 ──
builder.Services.AddScoped<LiffAuthFilter>();

// ── ASP.NET Core ──
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middleware 管線 ──
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}
app.MapControllers();

app.Run();
