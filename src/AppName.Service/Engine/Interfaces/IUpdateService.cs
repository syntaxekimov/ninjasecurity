using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo> CheckUpdatesAsync(CancellationToken ct = default);
    Task<bool> UpdateSignaturesAsync(CancellationToken ct = default);
    Task<bool> UpdateYaraRulesAsync(CancellationToken ct = default);
    Task<bool> UpdateMalSearcherDbAsync(CancellationToken ct = default);
    DateTime? LastUpdated { get; }
}
