namespace NinjaSecurity.Service.Engine.Interfaces;

public interface IYaraScanner
{
    Task<YaraResult> ScanAsync(string filePath, CancellationToken ct = default);
}

public record YaraResult(bool Matched, string? RuleName);
