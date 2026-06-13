namespace AppName.Service.Engine.Models;

public record AutorunEntry(
    string Id,
    string Name,
    string ImagePath,
    string Location,
    bool IsEnabled,
    bool IsSigned,
    int RiskScore
);
