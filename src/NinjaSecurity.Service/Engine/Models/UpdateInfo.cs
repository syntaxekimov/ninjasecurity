namespace NinjaSecurity.Service.Engine.Models;

public record UpdateInfo(
    bool SignaturesAvailable,
    bool YaraRulesAvailable,
    bool MalSearcherAvailable,
    DateTime? LastChecked
);
