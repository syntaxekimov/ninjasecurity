using AppName.Service.Engine.Models;

namespace AppName.Service.Engine;

public class ThreatScorer
{
    public int Score(IReadOnlyList<ScanResult> detections)
    {
        var total = detections.Sum(d => d.Source switch
        {
            DetectionSource.Hash   => 80,
            DetectionSource.ClamAv => 70,
            DetectionSource.Yara   => 50,
            _                      => 0
        });
        return Math.Min(total, 100);
    }

    public ThreatDisposition Disposition(int score) => score switch
    {
        >= 70 => ThreatDisposition.Block,
        >= 40 => ThreatDisposition.Monitor,
        _     => ThreatDisposition.Allow
    };
}
