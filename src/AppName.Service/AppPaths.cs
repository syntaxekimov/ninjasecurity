namespace AppName.Service;

public static class AppPaths
{
    public static string AppData =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppName");

    public static string DatabasePath => Path.Combine(AppData, "appname.db");
    public static string QuarantinePath => Path.Combine(AppData, "Quarantine");
    public static string DatabasesPath => Path.Combine(AppData, "Databases");
    public static string MalSearcherDbPath => Path.Combine(DatabasesPath, "malsearcher.db");
    public static string YaraRulesPath => Path.Combine(DatabasesPath, "yara");
}
