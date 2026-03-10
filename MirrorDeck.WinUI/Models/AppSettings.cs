namespace MirrorDeck.WinUI.Models;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Default";
    public bool StartMinimized { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool EnableAirPlayService { get; set; } = true;
    public bool EnableAndroidService { get; set; } = true;
    public bool RunMirroringInBackgroundOnly { get; set; }

    public string AirPlayName { get; set; } = "MirrorDeck Receiver";
    public bool AutoStartUxPlay { get; set; }
    public bool AutoRestartUxPlay { get; set; }
    public bool AirPlayRequirePinOnFirstConnect { get; set; }
    public string AirPlayFixedPin { get; set; } = string.Empty;
    public bool AirPlayFullscreen { get; set; }
    public bool AirPlayAudioEnabled { get; set; } = true;
    public string UxPlayExtraArgs { get; set; } = string.Empty;

    public string DefaultScrcpyProfile { get; set; } = "Standard";
    public bool AutoStartAndroidService { get; set; }
    public bool AutoRestartAndroidService { get; set; }
    public string AndroidTcpHost { get; set; } = "192.168.0.10";
    public int AndroidTcpPort { get; set; } = 5555;
    public int ScrcpyBitrateKbps { get; set; } = 8000;
    public int ScrcpyMaxFps { get; set; } = 60;
    public int ScrcpyMaxSize { get; set; } = 1920;
    public bool ScrcpyAlwaysOnTop { get; set; }

    public string ManagedToolsDirectory { get; set; } = string.Empty;
    public string SnapshotDirectory { get; set; } = string.Empty;
    public bool HasCompletedInitialModuleSetup { get; set; } = true;
    public string SnapshotShortcut { get; set; } = "Ctrl+Shift+S";
    public string PauseResumeShortcut { get; set; } = "Ctrl+Shift+P";
    public bool AutoUpdateDependencyCheck { get; set; } = true;
    public bool EnableWindowsNotifications { get; set; } = true;
    public string LogLevel { get; set; } = "Information";
}
