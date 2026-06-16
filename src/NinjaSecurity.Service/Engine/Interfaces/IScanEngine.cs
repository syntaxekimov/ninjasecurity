using NinjaSecurity.Service.Engine.Models;

namespace NinjaSecurity.Service.Engine.Interfaces;

public interface IScanEngine
{
    Task<ScanResult> ScanFileAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<ScanResult>> ScanDirectoryAsync(
        string dirPath, bool recursive = true, CancellationToken ct = default);
}
