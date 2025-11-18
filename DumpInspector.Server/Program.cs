using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using DumpInspector.Server.Services.Interfaces;
using DumpInspector.Server.Services.Implementations;
using DumpInspector.Server.Models;
using Microsoft.Extensions.Options;
using DumpInspector.Server.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// bind settings
builder.Services.Configure<CrashDumpSettings>(builder.Configuration.GetSection("CrashDumpSettings"));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// Allow frontend dev server (Vite) to call the API during development
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "AllowFrontendDev",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://127.0.0.1:5000", "http://localhost:5000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// register repositories and services
// Configure EF Core with MariaDB (connection string in appsettings)
var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "server=localhost;port=3306;user=root;password=root;database=dumpinspector";
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 33))));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDumpStorageService, DumpStorageService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ICrashDumpSettingsProvider, DbCrashDumpSettingsProvider>();
builder.Services.AddScoped<IPdbIngestionService, SymStorePdbIngestionService>();
builder.Services.AddSingleton<DumpInspector.Server.Services.Analysis.AnalysisSessionManager>();
// IPdbProvider will be resolved based on configuration at runtime
builder.Services.AddHttpClient<NasPdbProvider>();
builder.Services.AddScoped<IPdbProvider>(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<CrashDumpSettings>>().Value;
    if (cfg.UseNasForPdb)
    {
        return sp.GetRequiredService<NasPdbProvider>();
    }
    else
    {
        return new LocalPdbProvider(sp.GetRequiredService<IOptions<CrashDumpSettings>>());
    }
});
builder.Services.AddScoped<IAnalysisService, CdbAnalysisService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

// Configure the HTTP request pipeline.
var enableSwagger = builder.Configuration.GetValue<bool?>("Swagger:Enabled")
                      ?? app.Environment.IsDevelopment();

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS policy to allow Vite dev server during development
app.UseCors("AllowFrontendDev");

app.UseAuthorization();

app.MapControllers();

app.Map("/ws/analysis", async (HttpContext context, DumpInspector.Server.Services.Analysis.AnalysisSessionManager manager) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    var sessionId = context.Request.Query["id"].ToString();
    if (string.IsNullOrWhiteSpace(sessionId) || !manager.TryGetSession(sessionId, out var session))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Session not found");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await manager.StreamSessionAsync(session, socket, context.RequestAborted);
}).ExcludeFromDescription();

app.MapFallbackToFile("/index.html");
// Ensure DB created and initial admin user exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var cfg = scope.ServiceProvider.GetRequiredService<IOptions<CrashDumpSettings>>().Value;
    var adminName = "admin";
    var existing = await repo.GetByUsernameAsync(adminName);
        if (existing == null)
        {
            var initial = cfg.InitialAdminPassword ?? "Admin@123";
            await auth.CreateUserAsync(adminName, initial, true, null);
        }
    }

app.Run();
