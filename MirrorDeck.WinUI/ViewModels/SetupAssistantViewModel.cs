using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.ViewModels;

public class SetupAssistantViewModel : ObservableObject
{
    private readonly IBonjourService _bonjourService;
    private readonly IDependencyService _dependencyService;
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;
    private readonly ILoggingService _loggingService;

    private string _bonjourState = "Unbekannt";
    private string _uxPlayState = "Unbekannt";
    private string _scrcpyState = "Unbekannt";
    private string _adbState = "Unbekannt";
    private string _installerPath = "Nicht heruntergeladen";
    private string _status = "Bereit";
    private bool _installUxPlay = true;
    private bool _installScrcpy = true;
    private bool _installBonjourForAirPlay = true;
    private string _theme = "Dark";
    private bool _startMinimized;
    private bool _minimizeToTray = true;
    private bool _startWithWindows;
    private bool _autoDependencyCheck = true;
    private bool _enableWindowsNotifications = true;
    private bool _enableAirPlayService = true;
    private bool _enableAndroidService = true;
    private bool _showFirstStartOnNextLaunch;
    private string _snapshotShortcut = string.Empty;
    private string _pauseResumeShortcut = string.Empty;
    private string _settingsPath = string.Empty;
    private bool _isLoading;

    public string BonjourState
    {
        get => _bonjourState;
        set => SetProperty(ref _bonjourState, value);
    }

    public string InstallerPath
    {
        get => _installerPath;
        set => SetProperty(ref _installerPath, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string UxPlayState
    {
        get => _uxPlayState;
        set => SetProperty(ref _uxPlayState, value);
    }

    public string ScrcpyState
    {
        get => _scrcpyState;
        set => SetProperty(ref _scrcpyState, value);
    }

    public string AdbState
    {
        get => _adbState;
        set => SetProperty(ref _adbState, value);
    }

    public bool InstallUxPlay
    {
        get => _installUxPlay;
        set => SetProperty(ref _installUxPlay, value);
    }

    public bool InstallScrcpy
    {
        get => _installScrcpy;
        set => SetProperty(ref _installScrcpy, value);
    }

    public bool InstallBonjourForAirPlay
    {
        get => _installBonjourForAirPlay;
        set => SetProperty(ref _installBonjourForAirPlay, value);
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set
        {
            if (SetProperty(ref _startMinimized, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                if (value)
                {
                    _autoStartService.Enable();
                }
                else
                {
                    _autoStartService.Disable();
                }

                PersistSettingsImmediate();
            }
        }
    }

    public bool AutoDependencyCheck
    {
        get => _autoDependencyCheck;
        set
        {
            if (SetProperty(ref _autoDependencyCheck, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool EnableWindowsNotifications
    {
        get => _enableWindowsNotifications;
        set
        {
            if (SetProperty(ref _enableWindowsNotifications, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool EnableAirPlayService
    {
        get => _enableAirPlayService;
        set
        {
            if (SetProperty(ref _enableAirPlayService, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool EnableAndroidService
    {
        get => _enableAndroidService;
        set
        {
            if (SetProperty(ref _enableAndroidService, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public bool ShowFirstStartOnNextLaunch
    {
        get => _showFirstStartOnNextLaunch;
        set
        {
            if (SetProperty(ref _showFirstStartOnNextLaunch, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public string SnapshotShortcut
    {
        get => _snapshotShortcut;
        set
        {
            if (SetProperty(ref _snapshotShortcut, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public string PauseResumeShortcut
    {
        get => _pauseResumeShortcut;
        set
        {
            if (SetProperty(ref _pauseResumeShortcut, value))
            {
                PersistSettingsImmediate();
            }
        }
    }

    public string SettingsPath
    {
        get => _settingsPath;
        set => SetProperty(ref _settingsPath, value);
    }

    public IAsyncRelayCommand CheckBonjourCommand { get; }
    public IAsyncRelayCommand DownloadBonjourCommand { get; }
    public IAsyncRelayCommand InstallBonjourCommand { get; }
    public IAsyncRelayCommand StartBonjourCommand { get; }
    public IAsyncRelayCommand RestartBonjourCommand { get; }
    public IAsyncRelayCommand InstallSelectedModulesCommand { get; }
    public IAsyncRelayCommand SaveSettingsCommand { get; }
    public IAsyncRelayCommand RepairAirPlayCommand { get; }

    public SetupAssistantViewModel(
        IBonjourService bonjourService,
        IDependencyService dependencyService,
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        ILoggingService loggingService)
    {
        _bonjourService = bonjourService;
        _dependencyService = dependencyService;
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _loggingService = loggingService;

        CheckBonjourCommand = new AsyncRelayCommand(() => SafeExecuteAsync(CheckBonjourAsync, "Setup.CheckBonjour"));
        DownloadBonjourCommand = new AsyncRelayCommand(() => SafeExecuteAsync(DownloadBonjourAsync, "Setup.DownloadBonjour"));
        InstallBonjourCommand = new AsyncRelayCommand(() => SafeExecuteAsync(InstallBonjourAsync, "Setup.InstallBonjour"));
        StartBonjourCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StartBonjourAsync, "Setup.StartBonjour"));
        RestartBonjourCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartBonjourAsync, "Setup.RestartBonjour"));
        InstallSelectedModulesCommand = new AsyncRelayCommand(() => SafeExecuteAsync(InstallSelectedModulesAsync, "Setup.InstallModules"));
        SaveSettingsCommand = new AsyncRelayCommand(() => SafeExecuteAsync(SaveSettingsAsync, "Setup.SaveSettings"));
        RepairAirPlayCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RepairAirPlayAsync, "Setup.RepairAirPlay"));

        LoadSettings();
    }

    public Task SaveSettingsAsync()
    {
        PersistSettingsImmediate();
        Status = "Einstellungen gespeichert";
        return Task.CompletedTask;
    }

    private void LoadSettings()
    {
        _isLoading = true;
        var settings = _settingsService.Current;
        Theme = string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";
        StartMinimized = settings.StartMinimized;
        MinimizeToTray = settings.MinimizeToTrayOnClose;
        AutoDependencyCheck = settings.AutoUpdateDependencyCheck;
        EnableWindowsNotifications = settings.EnableWindowsNotifications;
        EnableAirPlayService = settings.EnableAirPlayService;
        EnableAndroidService = settings.EnableAndroidService;
        ShowFirstStartOnNextLaunch = !settings.HasCompletedInitialModuleSetup;
        SnapshotShortcut = settings.SnapshotShortcut;
        PauseResumeShortcut = settings.PauseResumeShortcut;
        StartWithWindows = _autoStartService.IsEnabled() || settings.StartWithWindows;
        SettingsPath = _settingsService.GetSettingsPath();
        _isLoading = false;
    }

    private void PersistSettingsImmediate()
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            var settings = _settingsService.Current;
            settings.Theme = Theme;
            settings.StartMinimized = StartMinimized;
            settings.MinimizeToTrayOnClose = MinimizeToTray;
            settings.AutoUpdateDependencyCheck = AutoDependencyCheck;
            settings.EnableWindowsNotifications = EnableWindowsNotifications;
            settings.EnableAirPlayService = EnableAirPlayService;
            settings.EnableAndroidService = EnableAndroidService;
            settings.HasCompletedInitialModuleSetup = !ShowFirstStartOnNextLaunch;
            settings.SnapshotShortcut = SnapshotShortcut?.Trim() ?? string.Empty;
            settings.PauseResumeShortcut = PauseResumeShortcut?.Trim() ?? string.Empty;
            settings.StartWithWindows = StartWithWindows;

            ThemeCoordinator.ApplyTheme(settings.Theme);

            _ = _settingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Immediate settings persist failed", ex, "Setup");
        }
    }

    public async Task CheckBonjourAsync()
    {
        var status = await _dependencyService.GetDashboardStatusAsync();
        var airPlayReady = status.UxPlay.IsInstalled && status.Bonjour.IsInstalled && status.Bonjour.IsRunning;
        var androidReady = status.Scrcpy.IsInstalled && status.Adb.IsInstalled;

        UxPlayState = airPlayReady
            ? "Bereit"
            : !status.UxPlay.IsInstalled
                ? "Fehlt"
                : !status.Bonjour.IsInstalled
                    ? "Fehlt"
                    : "Error";

        ScrcpyState = androidReady
            ? "Bereit"
            : !status.Scrcpy.IsInstalled
                ? "Fehlt"
                : "Fehlt";

        AdbState = status.Adb.IsInstalled ? "Bereit" : "Fehlt";

        var installed = await _bonjourService.IsInstalledAsync();
        var running = installed && await _bonjourService.IsRunningAsync();
        BonjourState = !installed ? "Fehlt" : running ? "Bereit" : "Error";
        Status = "Dienst-Status geprüft";
    }

    public async Task InstallSelectedModulesAsync()
    {
        var selection = new InstallSelection
        {
            InstallUxPlay = InstallUxPlay,
            InstallScrcpy = InstallScrcpy,
            InstallBonjourForAirPlay = InstallBonjourForAirPlay,
        };

        if (selection.IsEmpty)
        {
            Status = "Bitte mindestens ein Modul auswählen.";
            return;
        }

        var progress = new Progress<string>(message => Status = message);
        await _dependencyService.InstallSelectedAsync(selection, progress);
        await CheckBonjourAsync();
    }

    public async Task DownloadBonjourAsync()
    {
        Status = "Lade Bonjour-Installer herunter...";
        var path = await _bonjourService.DownloadInstallerAsync();
        InstallerPath = path ?? "Download fehlgeschlagen";
        Status = path is null ? "Download fehlgeschlagen" : "Installer heruntergeladen";
    }

    public async Task InstallBonjourAsync()
    {
        if (InstallerPath is "Nicht heruntergeladen" or "Download fehlgeschlagen")
        {
            Status = "Bitte zuerst Installer herunterladen";
            return;
        }

        Status = "Starte Installation mit Admin-Rechten...";
        var ok = await _bonjourService.InstallDownloadedInstallerAsync(InstallerPath);
        Status = ok ? "Installation abgeschlossen" : "Installation fehlgeschlagen oder abgebrochen";
        await CheckBonjourAsync();
    }

    public async Task StartBonjourAsync()
    {
        var ok = await _bonjourService.StartAsync();
        Status = ok ? "Bonjour gestartet" : "Bonjour konnte nicht gestartet werden";
        await CheckBonjourAsync();
    }

    public async Task RestartBonjourAsync()
    {
        var ok = await _bonjourService.RestartAsync();
        Status = ok ? "Bonjour neu gestartet" : "Neustart fehlgeschlagen";
        await CheckBonjourAsync();
    }

    public async Task RepairAirPlayAsync()
    {
        Status = "AirPlay-Reparatur: Bonjour wird geprüft...";

        var installed = await _bonjourService.IsInstalledAsync();
        if (!installed)
        {
            Status = "Bonjour fehlt. Lade Installer...";
            var installer = await _bonjourService.DownloadInstallerAsync();
            if (string.IsNullOrWhiteSpace(installer))
            {
                Status = "Bonjour-Download fehlgeschlagen.";
                await CheckBonjourAsync();
                return;
            }

            Status = "Installiere Bonjour (Admin-Bestätigung möglich)...";
            installed = await _bonjourService.InstallDownloadedInstallerAsync(installer);
            if (!installed)
            {
                Status = "Bonjour-Installation fehlgeschlagen oder abgebrochen.";
                await CheckBonjourAsync();
                return;
            }
        }

        if (!await _bonjourService.IsRunningAsync())
        {
            var started = await _bonjourService.StartAsync();
            Status = started ? "Bonjour gestartet. AirPlay sollte wieder funktionieren." : "Bonjour konnte nicht gestartet werden.";
        }
        else
        {
            Status = "Bonjour ist bereits aktiv. AirPlay sollte bereit sein.";
        }

        await CheckBonjourAsync();
    }

    private async Task SafeExecuteAsync(Func<Task> action, string operation)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Status = "Aktion fehlgeschlagen, App bleibt aktiv. Details in Logs.";
            _loggingService.LogError($"{operation} failed", ex, "Setup");
        }
    }
}
