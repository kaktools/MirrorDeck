using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;
using System.Diagnostics;
using System.IO;

namespace MirrorDeck.WinUI.ViewModels;

public class DashboardViewModel : ObservableObject
{
    private readonly IDependencyService _dependencyService;
    private readonly IUxPlayService _uxPlayService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IAdbService _adbService;
    private readonly IBonjourService _bonjourService;
    private readonly ILoggingService _loggingService;
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;
    private readonly ITrayService _trayService;

    private DashboardStatus _status = new();
    private string _setupProgress = "Ready";
    private bool _useAirPlay;
    private bool _useAndroid;
    private bool _autostartEnabled;
    private string _lastSnapshot = "Kein Snapshot";
    private string _lastActivity = "Keine Aktivität";
    private string _tcpHost = "192.168.0.10";
    private int _tcpPort = 5555;

    public DashboardStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(OverallStatusText));
                OnPropertyChanged(nameof(UxPlayStatusText));
                OnPropertyChanged(nameof(BonjourStatusText));
                OnPropertyChanged(nameof(ScrcpyStatusText));
                OnPropertyChanged(nameof(AdbStatusText));
                OnPropertyChanged(nameof(UxPlayVersionText));
                OnPropertyChanged(nameof(ScrcpyVersionText));
                OnPropertyChanged(nameof(ShowAirPlayControls));
                OnPropertyChanged(nameof(ShowAndroidControls));
                OnPropertyChanged(nameof(AirPlayControlsVisibility));
                OnPropertyChanged(nameof(AndroidControlsVisibility));
                OnPropertyChanged(nameof(GlobalActionsVisibility));
                OnPropertyChanged(nameof(IsPauseActive));
                OnPropertyChanged(nameof(PauseStatusVisibility));
                OnPropertyChanged(nameof(PauseResumeLabel));
                OnPropertyChanged(nameof(AirPlaySessionStatusText));
                OnPropertyChanged(nameof(AndroidSessionStatusText));
                OnPropertyChanged(nameof(OverallStatusChipBackground));
                OnPropertyChanged(nameof(OverallStatusChipBorderBrush));
                OnPropertyChanged(nameof(BonjourChipBackground));
                OnPropertyChanged(nameof(BonjourChipBorderBrush));
                OnPropertyChanged(nameof(UxPlayChipBackground));
                OnPropertyChanged(nameof(UxPlayChipBorderBrush));
                OnPropertyChanged(nameof(AdbChipBackground));
                OnPropertyChanged(nameof(AdbChipBorderBrush));
                OnPropertyChanged(nameof(ScrcpyChipBackground));
                OnPropertyChanged(nameof(ScrcpyChipBorderBrush));
                OnPropertyChanged(nameof(AirPlayStatusChipsVisibility));
                OnPropertyChanged(nameof(AndroidStatusChipsVisibility));
            }
        }
    }

    public string SetupProgress
    {
        get => _setupProgress;
        set => SetProperty(ref _setupProgress, value);
    }

    public bool UseAirPlay
    {
        get => _useAirPlay;
        set
        {
            if (SetProperty(ref _useAirPlay, value))
            {
                OnPropertyChanged(nameof(ShowAirPlayControls));
                OnPropertyChanged(nameof(AirPlayControlsVisibility));
                OnPropertyChanged(nameof(GlobalActionsVisibility));
                OnPropertyChanged(nameof(OverallStatusText));
            }
        }
    }

    public bool UseAndroid
    {
        get => _useAndroid;
        set
        {
            if (SetProperty(ref _useAndroid, value))
            {
                OnPropertyChanged(nameof(ShowAndroidControls));
                OnPropertyChanged(nameof(AndroidControlsVisibility));
                OnPropertyChanged(nameof(GlobalActionsVisibility));
                OnPropertyChanged(nameof(OverallStatusText));
            }
        }
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set
        {
            if (SetProperty(ref _autostartEnabled, value))
            {
                OnPropertyChanged(nameof(AutostartStatusText));
            }
        }
    }

    public string AutostartStatusText => AutostartEnabled ? "Aktiv" : "Inaktiv";

    public string LastSnapshot
    {
        get => _lastSnapshot;
        set => SetProperty(ref _lastSnapshot, value);
    }

    public string LastActivity
    {
        get => _lastActivity;
        set => SetProperty(ref _lastActivity, value);
    }

    public string TcpHost
    {
        get => _tcpHost;
        set
        {
            if (SetProperty(ref _tcpHost, value))
            {
                _settingsService.Current.AndroidTcpHost = value;
                SaveSettingsFireAndForget("Dashboard.TcpHost");
                OnPropertyChanged(nameof(TcpEndpoint));
            }
        }
    }

    public int TcpPort
    {
        get => _tcpPort;
        set
        {
            if (SetProperty(ref _tcpPort, value))
            {
                _settingsService.Current.AndroidTcpPort = value;
                SaveSettingsFireAndForget("Dashboard.TcpPort");
                OnPropertyChanged(nameof(TcpEndpoint));
            }
        }
    }

    public string TcpEndpoint => $"{TcpHost}:{TcpPort}";

    public string OverallStatusText
    {
        get
        {
            if (IsPauseActive)
            {
                return "PAUSE";
            }

            if (Status.UxPlay.IsRunning || Status.Scrcpy.IsRunning)
            {
                return "RUNNING";
            }

            if ((UseAirPlay && !IsAirPlayAvailable) || (UseAndroid && !IsAndroidAvailable))
            {
                return "ERROR";
            }

            if (UseAirPlay || UseAndroid)
            {
                return "IDLE";
            }

            return "IDLE";
        }
    }

    public bool IsAirPlayAvailable => Status.UxPlay.IsInstalled && Status.Bonjour.IsInstalled && Status.Bonjour.IsRunning;
    public bool IsAndroidAvailable => Status.Scrcpy.IsInstalled && Status.Adb.IsInstalled;
    public bool ShowAirPlayControls => UseAirPlay;
    public bool ShowAndroidControls => UseAndroid;
    public Visibility AirPlayControlsVisibility => ShowAirPlayControls ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AndroidControlsVisibility => ShowAndroidControls ? Visibility.Visible : Visibility.Collapsed;
    public Visibility GlobalActionsVisibility => (ShowAirPlayControls || ShowAndroidControls) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AirPlayStatusChipsVisibility => UseAirPlay ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AndroidStatusChipsVisibility => UseAndroid ? Visibility.Visible : Visibility.Collapsed;
    public bool IsPauseActive => _uxPlayService.IsPaused() || _scrcpyService.IsPaused();
    public Visibility PauseStatusVisibility => IsPauseActive ? Visibility.Visible : Visibility.Collapsed;
    public string PauseResumeLabel => IsPauseActive ? "Resume" : "Pause";
    public string AirPlaySessionStatusText => !IsAirPlayAvailable ? "Error" : (Status.UxPlay.IsRunning ? "Running" : "Idle");
    public string AndroidSessionStatusText => !IsAndroidAvailable ? "Error" : (Status.Scrcpy.IsRunning ? "Running" : "Idle");

    public Brush OverallStatusChipBackground => GetChipBackground(OverallStatusText);
    public Brush OverallStatusChipBorderBrush => GetChipBorder(OverallStatusText);
    public Brush BonjourChipBackground => GetChipBackground(BonjourStatusText);
    public Brush BonjourChipBorderBrush => GetChipBorder(BonjourStatusText);
    public Brush UxPlayChipBackground => GetChipBackground(UxPlayStatusText);
    public Brush UxPlayChipBorderBrush => GetChipBorder(UxPlayStatusText);
    public Brush AdbChipBackground => GetChipBackground(AdbStatusText);
    public Brush AdbChipBorderBrush => GetChipBorder(AdbStatusText);
    public Brush ScrcpyChipBackground => GetChipBackground(ScrcpyStatusText);
    public Brush ScrcpyChipBorderBrush => GetChipBorder(ScrcpyStatusText);

    public string UxPlayStatusText => Status.UxPlay.IsRunning ? "Running" : (Status.UxPlay.IsInstalled ? "Installed" : "Missing");
    public string BonjourStatusText => Status.Bonjour.IsRunning ? "Running" : (Status.Bonjour.IsInstalled ? "Installed" : "Missing");
    public string ScrcpyStatusText => Status.Scrcpy.IsRunning ? "Running" : (Status.Scrcpy.IsInstalled ? "Installed" : "Missing");
    public string AdbStatusText => Status.Adb.IsInstalled ? "Available" : "Missing";
    public string UxPlayVersionText => string.IsNullOrWhiteSpace(Status.UxPlay.Version) ? "Version unknown" : "v" + Status.UxPlay.Version;
    public string ScrcpyVersionText => string.IsNullOrWhiteSpace(Status.Scrcpy.Version) ? "Version unknown" : "v" + Status.Scrcpy.Version;

    public IAsyncRelayCommand RefreshAsyncCommand { get; }
    public IAsyncRelayCommand RunSetupAsyncCommand { get; }
    public IAsyncRelayCommand StartAirPlayAsyncCommand { get; }
    public IAsyncRelayCommand StopAirPlayAsyncCommand { get; }
    public IAsyncRelayCommand RestartAirPlayAsyncCommand { get; }
    public IAsyncRelayCommand StartAndroidUsbAsyncCommand { get; }
    public IAsyncRelayCommand RestartAndroidAsyncCommand { get; }
    public IAsyncRelayCommand StopAndroidAsyncCommand { get; }
    public IAsyncRelayCommand StopAllCommand { get; }
    public IAsyncRelayCommand RestartAllCommand { get; }
    public IAsyncRelayCommand StartAndroidTcpAsyncCommand { get; }
    public IAsyncRelayCommand DisconnectAndroidTcpAsyncCommand { get; }
    public IAsyncRelayCommand TogglePauseCommand { get; }
    public IAsyncRelayCommand SnapshotCommand { get; }
    public IAsyncRelayCommand SaveServiceSelectionCommand { get; }
    public IRelayCommand ToggleAutostartCommand { get; }

    public DashboardViewModel(
        IDependencyService dependencyService,
        IUxPlayService uxPlayService,
        IScrcpyService scrcpyService,
        IAdbService adbService,
        IBonjourService bonjourService,
        ILoggingService loggingService,
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        ITrayService trayService)
    {
        _dependencyService = dependencyService;
        _uxPlayService = uxPlayService;
        _scrcpyService = scrcpyService;
        _adbService = adbService;
        _bonjourService = bonjourService;
        _loggingService = loggingService;
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _trayService = trayService;

        UseAirPlay = _settingsService.Current.EnableAirPlayService;
        UseAndroid = _settingsService.Current.EnableAndroidService;
        TcpHost = _settingsService.Current.AndroidTcpHost;
        TcpPort = _settingsService.Current.AndroidTcpPort;
        AutostartEnabled = _autoStartService.IsEnabled();

        RefreshAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RefreshAsync, "Dashboard.Refresh"));
        RunSetupAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RunSetupAsync, "Dashboard.Setup"));
        StartAirPlayAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StartAirPlayAsync, "Dashboard.AirPlayStart"));
        StopAirPlayAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(async () => await _uxPlayService.StopAsync(), "Dashboard.AirPlayStop"));
        RestartAirPlayAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartAirPlayAsync, "Dashboard.AirPlayRestart"));
        StartAndroidUsbAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StartAndroidUsbAsync, "Dashboard.AndroidUsbStart"));
        RestartAndroidAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartAndroidAsync, "Dashboard.AndroidRestart"));
        StopAndroidAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StopAndroidAsync, "Dashboard.AndroidStop"));
        StopAllCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StopAllAsync, "Dashboard.StopAll"));
        RestartAllCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartAllAsync, "Dashboard.RestartAll"));
        StartAndroidTcpAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StartAndroidTcpAsync, "Dashboard.AndroidTcpStart"));
        DisconnectAndroidTcpAsyncCommand = new AsyncRelayCommand(() => SafeExecuteAsync(DisconnectAndroidTcpAsync, "Dashboard.AndroidTcpDisconnect"));
        TogglePauseCommand = new AsyncRelayCommand(() => SafeExecuteAsync(TogglePauseResumeAllAsync, "Dashboard.TogglePause"));
        SnapshotCommand = new AsyncRelayCommand(() => SafeExecuteAsync(CaptureSnapshotAsync, "Dashboard.Snapshot"));
        SaveServiceSelectionCommand = new AsyncRelayCommand(() => SafeExecuteAsync(SaveServiceSelectionAsync, "Dashboard.SaveSelection"));
        ToggleAutostartCommand = new RelayCommand(ToggleAutostart);

        SeedPreviewState();
    }

    public async Task RefreshAsync()
    {
        UseAirPlay = _settingsService.Current.EnableAirPlayService;
        UseAndroid = _settingsService.Current.EnableAndroidService;

        Status = await _dependencyService.GetDashboardStatusAsync();
        Status.UxPlay.Message = UseAirPlay ? Status.UxPlay.Message : "Deaktiviert";
        Status.Scrcpy.Message = UseAndroid ? Status.Scrcpy.Message : "Deaktiviert";
        await _trayService.RefreshStatusAsync();

        OnPropertyChanged(nameof(AirPlayStatusChipsVisibility));
        OnPropertyChanged(nameof(AndroidStatusChipsVisibility));
    }

    public async Task RunSetupAsync()
    {
        var progress = new Progress<string>(message =>
        {
            SetupProgress = message;
            _loggingService.LogInfo(message, "DependencySetup");
        });

        var selection = new InstallSelection
        {
            InstallUxPlay = UseAirPlay,
            InstallScrcpy = UseAndroid,
            InstallBonjourForAirPlay = UseAirPlay,
        };

        await _dependencyService.InstallSelectedAsync(selection, progress);
        LastActivity = "Setup check completed";
        await RefreshAsync();
    }

    public async Task StopAllAsync()
    {
        await _uxPlayService.StopAsync();
        await _scrcpyService.StopAsync();
        LastActivity = "All sessions stopped";
        await RefreshAsync();
    }

    private async Task RestartAllAsync()
    {
        if (ShowAirPlayControls)
        {
            if (_uxPlayService.IsRunning())
            {
                await _uxPlayService.RestartAsync();
            }
            else
            {
                await StartAirPlayAsync();
            }
        }

        if (ShowAndroidControls)
        {
            if (_scrcpyService.IsRunning())
            {
                await RestartAndroidAsync();
            }
            else
            {
                await StartAndroidTcpAsync();
            }
        }

        LastActivity = "Restart all requested";
        await RefreshAsync();
    }

    private async Task SaveServiceSelectionAsync()
    {
        _settingsService.Current.EnableAirPlayService = UseAirPlay;
        _settingsService.Current.EnableAndroidService = UseAndroid;
        await _settingsService.SaveAsync();
        await RefreshAsync();
    }

    private void ToggleAutostart()
    {
        if (AutostartEnabled)
        {
            _autoStartService.Disable();
        }
        else
        {
            _autoStartService.Enable();
        }

        AutostartEnabled = _autoStartService.IsEnabled();
    }

    private async Task StartAirPlayAsync()
    {
        if (!UseAirPlay)
        {
            SetupProgress = "AirPlay ist deaktiviert.";
            return;
        }

        if (!_uxPlayService.IsRunning() && !File.Exists(_uxPlayService.GetExecutablePath()))
        {
            SetupProgress = "AirPlay kann nicht starten: UxPlay fehlt. Bitte Setup-Assistent ausführen.";
            return;
        }

        var bonjourInstalled = await _bonjourService.IsInstalledAsync();
        if (!bonjourInstalled)
        {
            SetupProgress = "AirPlay kann nicht starten: Bonjour fehlt. Bitte Setup-Assistent ausführen.";
            return;
        }

        if (!await _bonjourService.IsRunningAsync())
        {
            SetupProgress = "AirPlay kann nicht starten: Bonjour-Dienst ist gestoppt.";
            return;
        }

        try
        {
            await _uxPlayService.StartAsync();
            LastActivity = "AirPlay start requested";
            await _trayService.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            SetupProgress = "AirPlay konnte nicht gestartet werden. Bitte Setup-Assistent prüfen.";
            _loggingService.LogError("StartAirPlay failed", ex, "Dashboard");
        }
    }

    private async Task StartAndroidUsbAsync()
    {
        if (!UseAndroid)
        {
            SetupProgress = "Android ist deaktiviert.";
            return;
        }

        if (!File.Exists(_scrcpyService.GetExecutablePath()))
        {
            SetupProgress = "Android kann nicht starten: scrcpy fehlt. Bitte Setup-Assistent ausführen.";
            return;
        }

        if (!await _adbService.IsAvailableAsync())
        {
            SetupProgress = "Android kann nicht starten: adb fehlt. Bitte Setup-Assistent ausführen.";
            return;
        }

        try
        {
            await _scrcpyService.StartAsync(new ScrcpyProfile { Name = "Standard", EnableAudio = false }, hiddenOverride: false);
            LastActivity = "Android USB start requested";
            await _trayService.RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            SetupProgress = "Android konnte nicht gestartet werden. Bitte Setup-Assistent prüfen.";
            _loggingService.LogError("StartAndroidUsb failed", ex, "Dashboard");
        }
    }

    private async Task StopAndroidAsync()
    {
        await _scrcpyService.StopAsync();
        LastActivity = "Android stop requested";
        await RefreshAsync();
    }

    private async Task RestartAndroidAsync()
    {
        if (!IsAndroidAvailable)
        {
            SetupProgress = "Android ist nicht verfügbar.";
            return;
        }

        var profile = new ScrcpyProfile { Name = "Standard", EnableAudio = false };
        await _scrcpyService.RestartAsync(profile, hiddenOverride: false);
        LastActivity = "Android restart requested";
        await RefreshAsync();
    }

    private async Task RestartAirPlayAsync()
    {
        if (!IsAirPlayAvailable)
        {
            SetupProgress = "AirPlay ist nicht verfügbar.";
            return;
        }

        await _uxPlayService.RestartAsync();
        LastActivity = "AirPlay restart requested";
        await RefreshAsync();
    }

    private async Task StartAndroidTcpAsync()
    {
        if (!UseAndroid)
        {
            SetupProgress = "Android ist deaktiviert.";
            return;
        }

        if (!File.Exists(_scrcpyService.GetExecutablePath()))
        {
            SetupProgress = "Android kann nicht starten: scrcpy fehlt. Bitte Setup-Assistent ausführen.";
            return;
        }

        if (!await _adbService.IsAvailableAsync())
        {
            SetupProgress = "Android kann nicht starten: adb fehlt. Bitte Setup-Assistent ausführen.";
            return;
        }

        var tcpEndpoint = TcpEndpoint;
        var devices = await _adbService.GetConnectedDevicesAsync();
        var alreadyConnected = devices.Any(d => string.Equals(d, tcpEndpoint, StringComparison.OrdinalIgnoreCase));

        if (!alreadyConnected)
        {
            var connectResult = await _adbService.ConnectTcpWithOutputAsync(TcpHost, TcpPort);
            if (!connectResult.Success)
            {
                var hasUsbDevice = devices.Any(d => !d.Contains(':'));
                if (hasUsbDevice)
                {
                    await _adbService.EnableTcpIpModeAsync(TcpPort);
                    await Task.Delay(700);
                    connectResult = await _adbService.ConnectTcpWithOutputAsync(TcpHost, TcpPort);
                }
            }

            if (!connectResult.Success)
            {
                SetupProgress = $"TCP-Verbindung fehlgeschlagen ({tcpEndpoint}).";
                return;
            }
        }

        await _scrcpyService.StartAsync(new ScrcpyProfile
        {
            Name = "Standard",
            EnableAudio = false,
            DeviceSerial = tcpEndpoint
        }, hiddenOverride: false);

        LastActivity = $"Android TCP start requested ({tcpEndpoint})";
        await RefreshAsync();
    }

    private async Task DisconnectAndroidTcpAsync()
    {
        await _adbService.DisconnectTcpAsync(TcpHost, TcpPort);
        LastActivity = $"Android TCP disconnected ({TcpEndpoint})";
        await RefreshAsync();
    }

    private async Task TogglePauseResumeAllAsync()
    {
        if (!_uxPlayService.IsRunning() && !_scrcpyService.IsRunning())
        {
            SetupProgress = "Keine aktive Session zum Pausieren/Fortsetzen.";
            return;
        }

        var pauseRequested = !IsPauseActive;
        if (_uxPlayService.IsRunning())
        {
            if ((pauseRequested && !_uxPlayService.IsPaused()) || (!pauseRequested && _uxPlayService.IsPaused()))
            {
                await _uxPlayService.TogglePauseAsync();
            }
        }

        if (_scrcpyService.IsRunning())
        {
            if ((pauseRequested && !_scrcpyService.IsPaused()) || (!pauseRequested && _scrcpyService.IsPaused()))
            {
                await _scrcpyService.TogglePauseAsync();
            }
        }

        LastActivity = pauseRequested ? "Sessions paused" : "Sessions resumed";
        await RefreshAsync();
        OnPropertyChanged(nameof(IsPauseActive));
        OnPropertyChanged(nameof(PauseStatusVisibility));
        OnPropertyChanged(nameof(PauseResumeLabel));
    }

    private async Task CaptureSnapshotAsync()
    {
        try
        {
            var snapshotFiles = new List<string>();

            var airPlaySnapshot = await _uxPlayService.CaptureSnapshotAsync();
            if (!string.IsNullOrWhiteSpace(airPlaySnapshot) && File.Exists(airPlaySnapshot))
            {
                snapshotFiles.Add(airPlaySnapshot);
            }

            var androidSnapshot = await _scrcpyService.CaptureSnapshotAsync();
            if (!string.IsNullOrWhiteSpace(androidSnapshot) && File.Exists(androidSnapshot))
            {
                snapshotFiles.Add(androidSnapshot);
            }

            if (snapshotFiles.Count == 0)
            {
                LastSnapshot = "Kein Fenster für Snapshot verfügbar";
                LastActivity = "Snapshot skipped";
                return;
            }

            foreach (var file in snapshotFiles)
            {
                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = true
                });
            }

            LastSnapshot = string.Join(" | ", snapshotFiles);
            LastActivity = "Snapshot captured";
        }
        catch (Exception ex)
        {
            LastSnapshot = "Snapshot fehlgeschlagen";
            _loggingService.LogError("Snapshot failed", ex, "Dashboard");
        }

        OnPropertyChanged(nameof(IsPauseActive));
        OnPropertyChanged(nameof(PauseStatusVisibility));
        OnPropertyChanged(nameof(PauseResumeLabel));
        OnPropertyChanged(nameof(OverallStatusText));
    }

    private async Task SafeExecuteAsync(Func<Task> action, string operation)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetupProgress = "Aktion fehlgeschlagen, App bleibt aktiv. Details in Logs.";
            _loggingService.LogError($"{operation} failed", ex, "Dashboard");
        }
    }

    private void SaveSettingsFireAndForget(string source)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Settings save failed", ex, source);
            }
        });
    }

    private void SeedPreviewState()
    {
        Status = new DashboardStatus
        {
            UxPlay = new ToolStatus { IsInstalled = true, IsRunning = false, Message = "Ready", Version = "1.73" },
            Bonjour = new ToolStatus { IsInstalled = true, IsRunning = true, Message = "Running", Version = "Service" },
            Scrcpy = new ToolStatus { IsInstalled = true, IsRunning = false, Message = "Ready", Version = "3.3.4" },
            Adb = new ToolStatus { IsInstalled = true, IsRunning = false, Message = "Available", Version = "1.0.41" },
            LastConnection = "None",
            LastMode = "USB"
        };
    }

    private static Brush GetChipBackground(string? state)
    {
        return (state ?? string.Empty).ToUpperInvariant() switch
        {
            "RUNNING" => new SolidColorBrush(ColorHelper.FromArgb(0x33, 0x2F, 0xBF, 0x78)),
            "AVAILABLE" => new SolidColorBrush(ColorHelper.FromArgb(0x2B, 0x3C, 0xA8, 0xFF)),
            "PAUSE" => new SolidColorBrush(ColorHelper.FromArgb(0x33, 0xD7, 0x98, 0x32)),
            "ERROR" => new SolidColorBrush(ColorHelper.FromArgb(0x36, 0xE4, 0x5A, 0x6E)),
            "MISSING" => new SolidColorBrush(ColorHelper.FromArgb(0x36, 0xE4, 0x5A, 0x6E)),
            "INSTALLED" => new SolidColorBrush(ColorHelper.FromArgb(0x20, 0x8C, 0xA2, 0xC0)),
            "IDLE" => new SolidColorBrush(ColorHelper.FromArgb(0x20, 0x8C, 0xA2, 0xC0)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)),
        };
    }

    private static Brush GetChipBorder(string? state)
    {
        return (state ?? string.Empty).ToUpperInvariant() switch
        {
            "RUNNING" => new SolidColorBrush(ColorHelper.FromArgb(0x66, 0x2F, 0xBF, 0x78)),
            "AVAILABLE" => new SolidColorBrush(ColorHelper.FromArgb(0x66, 0x3C, 0xA8, 0xFF)),
            "PAUSE" => new SolidColorBrush(ColorHelper.FromArgb(0x66, 0xD7, 0x98, 0x32)),
            "ERROR" => new SolidColorBrush(ColorHelper.FromArgb(0x77, 0xE4, 0x5A, 0x6E)),
            "MISSING" => new SolidColorBrush(ColorHelper.FromArgb(0x77, 0xE4, 0x5A, 0x6E)),
            "INSTALLED" => new SolidColorBrush(ColorHelper.FromArgb(0x55, 0x8C, 0xA2, 0xC0)),
            "IDLE" => new SolidColorBrush(ColorHelper.FromArgb(0x55, 0x8C, 0xA2, 0xC0)),
            _ => new SolidColorBrush(ColorHelper.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
        };
    }
}
