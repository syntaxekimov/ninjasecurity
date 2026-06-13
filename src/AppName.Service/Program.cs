using AppName.Service;
using AppName.Service.Data;
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Ipc;
using Microsoft.EntityFrameworkCore;

Directory.CreateDirectory(AppPaths.AppData);
Directory.CreateDirectory(AppPaths.QuarantinePath);
Directory.CreateDirectory(AppPaths.DatabasesPath);
Directory.CreateDirectory(AppPaths.YaraRulesPath);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(opt => opt.ServiceName = "AppName Security");

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

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Auto-migrate database on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
