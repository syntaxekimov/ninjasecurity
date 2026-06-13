using AppName.Service.Engine.Interfaces;
using dnYara;

namespace AppName.Service.Engine;

public class YaraScanner : IYaraScanner
{
    private readonly string _rulesPath;

    public YaraScanner(string? rulesPath = null)
    {
        _rulesPath = rulesPath ?? AppPaths.YaraRulesPath;
    }

    public Task<YaraResult> ScanAsync(string filePath, CancellationToken ct = default)
    {
        if (!Directory.Exists(_rulesPath))
            return Task.FromResult(new YaraResult(false, null));

        var ruleFiles = Directory.GetFiles(_rulesPath, "*.yar", SearchOption.AllDirectories);
        if (ruleFiles.Length == 0)
            return Task.FromResult(new YaraResult(false, null));

        try
        {
            using var ctx = new YaraContext();
            using var compiler = new Compiler();
            foreach (var ruleFile in ruleFiles)
                compiler.AddRuleFile(ruleFile);

            using var rules = compiler.Compile();
            var scanner = new Scanner();
            var matches = scanner.ScanFile(filePath, rules);

            var first = matches.FirstOrDefault();
            return Task.FromResult(first != null
                ? new YaraResult(true, first.MatchingRule.Identifier)
                : new YaraResult(false, null));
        }
        catch
        {
            return Task.FromResult(new YaraResult(false, null));
        }
    }
}
