using AppName.Service.Data.Entities;
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IQuarantineManager
{
    Task<Guid> IsolateAsync(string filePath, ScanResult threat, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken ct = default);
}
