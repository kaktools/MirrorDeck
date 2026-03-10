namespace MirrorDeck.WinUI.Infrastructure;

public static class AppPaths
{
    public static string RootAppData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MirrorDeck");
    public static string SettingsFile => Path.Combine(RootAppData, "settings.json");
    public static string LogDirectory => Path.Combine(RootAppData, "logs");
    public static string LogFile => Path.Combine(LogDirectory, $"mirrordeck-{DateTime.UtcNow:yyyyMMdd}.log");
    public static string LegacyLogFile => Path.Combine(RootAppData, "mirrordeck.log");
    public static string SessionMarkerFile => Path.Combine(RootAppData, "session-active.json");
    public static string ToolsRoot => Path.Combine(RootAppData, "tools");
    public static string UxPlayRoot => Path.Combine(ToolsRoot, "uxplay");
    public static string ScrcpyRoot => Path.Combine(ToolsRoot, "scrcpy");
    public static string SnapshotRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "MirrorDeck");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootAppData);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(ToolsRoot);
        Directory.CreateDirectory(UxPlayRoot);
        Directory.CreateDirectory(ScrcpyRoot);
        Directory.CreateDirectory(SnapshotRoot);
    }
}
