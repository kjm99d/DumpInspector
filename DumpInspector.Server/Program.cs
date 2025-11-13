using System;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// bind settings
builder.Services.Configure<DumpInspector.Server.Models.CrashDumpSettings>(builder.Configuration.GetSection("CrashDumpSettings"));

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddDbContext<DumpInspector.Server.Data.AppDbContext>(opt =>
    opt.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 33))));

builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IUserRepository, DumpInspector.Server.Services.Implementations.EfUserRepository>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IAuthService, DumpInspector.Server.Services.Implementations.AuthService>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IAdminService, DumpInspector.Server.Services.Implementations.AdminService>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IDumpStorageService, DumpInspector.Server.Services.Implementations.DumpStorageService>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IEmailSender, DumpInspector.Server.Services.Implementations.SmtpEmailSender>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.ICrashDumpSettingsProvider, DumpInspector.Server.Services.Implementations.DbCrashDumpSettingsProvider>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IPdbIngestionService, DumpInspector.Server.Services.Implementations.SymStorePdbIngestionService>();
builder.Services.AddSingleton<DumpInspector.Server.Services.Analysis.AnalysisSessionManager>();
// IPdbProvider will be resolved based on configuration at runtime
builder.Services.AddHttpClient<DumpInspector.Server.Services.Implementations.NasPdbProvider>();
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IPdbProvider>(sp =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings>>().Value;
    if (cfg.UseNasForPdb)
    {
        return sp.GetRequiredService<DumpInspector.Server.Services.Implementations.NasPdbProvider>();
    }
    else
    {
        return new DumpInspector.Server.Services.Implementations.LocalPdbProvider(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings>>());
    }
});
builder.Services.AddScoped<DumpInspector.Server.Services.Interfaces.IAnalysisService, DumpInspector.Server.Services.Implementations.CdbAnalysisService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
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
});

app.MapFallbackToFile("/index.html");
// Ensure DB created and initial admin user exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DumpInspector.Server.Data.AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var repo = scope.ServiceProvider.GetRequiredService<DumpInspector.Server.Services.Interfaces.IUserRepository>();
    var auth = scope.ServiceProvider.GetRequiredService<DumpInspector.Server.Services.Interfaces.IAuthService>();
    var cfg = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DumpInspector.Server.Models.CrashDumpSettings>>().Value;
    var adminName = "admin";
    var existing = await repo.GetByUsernameAsync(adminName);
        if (existing == null)
        {
            var initial = cfg.InitialAdminPassword ?? "Admin@123";
            await auth.CreateUserAsync(adminName, initial, true, null);
        }
    }

app.Run();
