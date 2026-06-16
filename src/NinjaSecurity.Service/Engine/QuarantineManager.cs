using NinjaSecurity.Service.Data;
using NinjaSecurity.Service.Data.Entities;
using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Engine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;

namespace NinjaSecurity.Service.Engine;

public class QuarantineManager : IQuarantineManager
{
    private static readonly string[] ProtectedPrefixes = BuildProtectedPrefixes();

    private static string[] BuildProtectedPrefixes() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft"),
    ];

    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<QuarantineManager> _logger;
    private readonly string _quarantinePath;

    public QuarantineManager(AppDbContext db, IDataProtector protector, ILogger<QuarantineManager> logger, string? quarantinePath = null)
    {
        _db = db;
        _protector = protector;
        _logger = logger;
        _quarantinePath = quarantinePath ?? AppPaths.QuarantinePath;
        Directory.CreateDirectory(_quarantinePath);
    }

    public async Task<Guid> IsolateAsync(string filePath, ScanResult threat, CancellationToken ct = default)
    {
        var sha256 = await ComputeSha256Async(filePath, ct);
        var id = Guid.NewGuid();
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(16);
        var encryptedFileName = $"{id:N}.qvault";
        var quarantineFilePath = Path.Combine(_quarantinePath, encryptedFileName);

        await EncryptFileAsync(filePath, quarantineFilePath, aesKey, iv, ct);
        File.Delete(filePath);

        _db.QuarantineEntries.Add(new QuarantineEntry
        {
            Id = id,
            OriginalPath = filePath,
            ThreatName = threat.ThreatName ?? "Unknown",
            ConfidenceScore = threat.ConfidenceScore,
            Sha256 = sha256,
            QuarantinedAt = DateTime.UtcNow,
            EncryptedFileName = encryptedFileName,
            ProtectedKey = _protector.Protect(aesKey),
            Iv = iv
        });
        await _db.SaveChangesAsync(ct);
        return id;
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.QuarantineEntries.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Quarantine entry {id} not found");

        ValidateRestorePath(entry.OriginalPath);

        var aesKey = _protector.Unprotect(entry.ProtectedKey);
        var quarantineFilePath = Path.Combine(_quarantinePath, entry.EncryptedFileName);

        _logger.LogInformation(
            "Security audit: restoring quarantine entry {Id} to {Path}",
            id, entry.OriginalPath);

        if (OperatingSystem.IsWindows())
        {
            EventLog.WriteEntry("NinjaSecurity",
                $"Quarantine restore: entry={id} path={entry.OriginalPath}",
                EventLogEntryType.Information, eventID: 1001);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
        await DecryptFileAsync(quarantineFilePath, entry.OriginalPath, aesKey, entry.Iv, ct);
        File.Delete(quarantineFilePath);

        _db.QuarantineEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    private static void ValidateRestorePath(string originalPath)
    {
        if (string.IsNullOrWhiteSpace(originalPath))
            throw new InvalidOperationException("Restore path is empty");

        string fullPath;
        try { fullPath = Path.GetFullPath(originalPath); }
        catch { throw new InvalidOperationException($"Invalid restore path: {originalPath}"); }

        if (!Path.IsPathRooted(fullPath) || fullPath.StartsWith(@"\\"))
            throw new InvalidOperationException($"Restore to UNC or relative path is not allowed: {originalPath}");

        foreach (var prefix in ProtectedPrefixes)
        {
            if (!string.IsNullOrEmpty(prefix) &&
                fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Restore to protected system path '{fullPath}' is not allowed");
            }
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.QuarantineEntries.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Quarantine entry {id} not found");

        var quarantineFilePath = Path.Combine(_quarantinePath, entry.EncryptedFileName);
        if (File.Exists(quarantineFilePath))
            File.Delete(quarantineFilePath);

        _db.QuarantineEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken ct = default) =>
        await _db.QuarantineEntries.OrderByDescending(e => e.QuarantinedAt).ToListAsync(ct);

    private static async Task EncryptFileAsync(
        string source, string dest, byte[] key, byte[] iv, CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using var sourceStream = File.OpenRead(source);
        await using var destStream = File.Create(dest);
        await using var cryptoStream = new CryptoStream(destStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await sourceStream.CopyToAsync(cryptoStream, ct);
    }

    private static async Task DecryptFileAsync(
        string source, string dest, byte[] key, byte[] iv, CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using var sourceStream = File.OpenRead(source);
        await using var cryptoStream = new CryptoStream(sourceStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        await using var destStream = File.Create(dest);
        await cryptoStream.CopyToAsync(destStream, ct);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
