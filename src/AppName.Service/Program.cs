using NinjaSecurity.Service;
using NinjaSecurity.Service.Data;
using NinjaSecurity.Service.Engine;
using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Ipc;
using Microsoft.EntityFrameworkCore;

Directory.CreateDirectory(AppPaths.AppData);
Directory.CreateDirectory(AppPaths.QuarantinePath);
Directory.CreateDirectory(AppPaths.DatabasesPath);
Directory.CreateDirectory(AppPaths.YaraRulesPath);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(opt => opt.ServiceName = "Ninja Security");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

// Singletons: stateless or thread-safe engine components
builder.Services.AddSingleton<IDataProtector, DpapiDataProtector>();
builder.Services.AddSingleton<HashChecker>();
builder.Services.AddSingleton<IClamAvScanner, ClamAvScanner>();
builder.Services.AddSingleton<IYaraScanner, YaraScanner>();
builder.Services.AddSingleton<ThreatScorer>();
builder.Services.AddSingleton<IScanEngine, ScanEngine>();
builder.Services.AddSingleton<IpcServer>(); // uses IServiceScopeFactory internally

// Scoped: one DbContext per IPC request scope
builder.Services.AddScoped<IQuarantineManager, QuarantineManager>();
builder.Services.AddScoped<CommandHandler>(); // resolved per-scope inside IpcServer

// Plan 2 — real-time protection and system tools
builder.Services.AddSingleton<IRealTimeGuard>(sp => new RealTimeGuard(
    sp.GetRequiredService<IScanEngine>(),
    sp.GetRequiredService<IServiceScopeFactory>(),
    logger: sp.GetService<ILogger<RealTimeGuard>>(),
    ransomwareDetector: sp.GetService<RansomwareDetector>()));
builder.Services.AddSingleton<IProcessMonitor, ProcessMonitor>();
builder.Services.AddSingleton<ISystemOptimizer, SystemOptimizer>();
builder.Services.AddSingleton<IUpdateService, UpdateService>();
builder.Services.AddSingleton<RansomwareDetector>();
builder.Services.AddSingleton<IpcEventChannel>();
builder.Services.AddHttpClient<UpdateService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Auto-migrate database on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
