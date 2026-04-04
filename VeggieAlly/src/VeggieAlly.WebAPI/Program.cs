using VeggieAlly.Application;
using VeggieAlly.Infrastructure;

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
