namespace AppName.Service;

public static class AppPaths
{
    public static readonly string AppData =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppName");

    public static readonly string DatabasePath = Path.Combine(AppData, "appname.db");
    public static readonly string QuarantinePath = Path.Combine(AppData, "Quarantine");
    public static readonly string DatabasesPath = Path.Combine(AppData, "Databases");
    public static readonly string MalSearcherDbPath = Path.Combine(DatabasesPath, "malsearcher.db");
    public static readonly string YaraRulesPath = Path.Combine(DatabasesPath, "yara");
}
