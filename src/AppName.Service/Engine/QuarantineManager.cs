using AppName.Service.Data;
using AppName.Service.Data.Entities;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AppName.Service.Engine;

public class QuarantineManager : IQuarantineManager
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly string _quarantinePath;

    public QuarantineManager(AppDbContext db, IDataProtector protector, string? quarantinePath = null)
    {
        _db = db;
        _protector = protector;
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

        var aesKey = _protector.Unprotect(entry.ProtectedKey);
        var quarantineFilePath = Path.Combine(_quarantinePath, entry.EncryptedFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
        await DecryptFileAsync(quarantineFilePath, entry.OriginalPath, aesKey, entry.Iv, ct);
        File.Delete(quarantineFilePath);

        _db.QuarantineEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
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
