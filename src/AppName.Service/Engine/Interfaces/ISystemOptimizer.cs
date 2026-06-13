using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface ISystemOptimizer
{
    Task<IReadOnlyList<AutorunEntry>> GetAutostartEntriesAsync(CancellationToken ct = default);
    Task SetAutostartEnabledAsync(string entryId, bool enabled, CancellationToken ct = default);
    Task<long> GetTempFileSizeAsync(CancellationToken ct = default);
    Task<long> CleanTempFilesAsync(CancellationToken ct = default);
}
