namespace NinjaSecurity.Service.Engine.Models;

public record AppUpdateInfo(
    bool UpdateAvailable,
    string? LatestVersion,
    string? DownloadUrl,
    string? ReleaseNotes);
