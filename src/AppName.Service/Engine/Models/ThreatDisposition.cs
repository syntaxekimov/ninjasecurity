namespace AppName.Service.Engine.Models;

public enum ThreatDisposition
{
    Allow,    // score < 40
    Monitor,  // score 40–69
    Block     // score >= 70
}
