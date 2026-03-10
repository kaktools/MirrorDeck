using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;
using Windows.ApplicationModel.DataTransfer;

namespace MirrorDeck.WinUI.ViewModels;

public class AndroidViewModel : ObservableObject
{
    private const int MaxLiveLogLength = 32000;
    private const int MaxAutoRestartAttempts = 10;
    private static readonly TimeSpan AutoRestartDelay = TimeSpan.FromSeconds(5);

    private readonly IAdbService _adbService;
    private readonly IScrcpyService _scrcpyService;
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly SemaphoreSlim _autoRestartGate = new(1, 1);

    private string _tcpHost = "192.168.0.10";
    private int _tcpPort = 5555;
    private string _selectedProfile = "Standard";
    private string _liveLog = "scrcpy/adb logs appear here...";
    private IReadOnlyList<string> _devices = [];
    private bool _autoStartOnAppStart;
    private bool _autoRestartOnFailure;
    private bool _manualStopRequested;
    private int _autoRestartAttempts;
    private bool _autoRestartLimitReachedLogged;

    public string TcpHost
    {
        get => _tcpHost;
        set
        {
            if (SetProperty(ref _tcpHost, value))
            {
                _settingsService.Current.AndroidTcpHost = value;
                SaveSettingsFireAndForget("Android.TcpHost");
                OnPropertyChanged(nameof(TcpEndpoint));
                OnPropertyChanged(nameof(TcpStatusText));
            }
        }
    }

    public int TcpPort
    {
        get => _tcpPort;
        set
        {
            var normalized = Math.Clamp(value, 1, 65535);
            if (SetProperty(ref _tcpPort, normalized))
            {
                _settingsService.Current.AndroidTcpPort = normalized;
                SaveSettingsFireAndForget("Android.TcpPort");
                OnPropertyChanged(nameof(TcpEndpoint));
                OnPropertyChanged(nameof(TcpStatusText));
            }
        }
    }

    public string SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public string LiveLog
    {
        get => _liveLog;
        set => SetProperty(ref _liveLog, value);
    }

    public IReadOnlyList<string> Devices
    {
        get => _devices;
        set
        {
            if (SetProperty(ref _devices, value))
            {
                OnPropertyChanged(nameof(DeviceCountText));
                OnPropertyChanged(nameof(TcpStatusText));
                OnPropertyChanged(nameof(OverallStatusText));
            }
        }
    }

    public bool AutoStartOnAppStart
    {
        get => _autoStartOnAppStart;
        set
        {
            if (SetProperty(ref _autoStartOnAppStart, value))
            {
                _settingsService.Current.AutoStartAndroidService = value;
                SaveSettingsFireAndForget("Android.AutoStart");
            }
        }
    }

    public bool AutoRestartOnFailure
    {
        get => _autoRestartOnFailure;
        set
        {
            if (SetProperty(ref _autoRestartOnFailure, value))
            {
                _settingsService.Current.AutoRestartAndroidService = value;
                SaveSettingsFireAndForget("Android.AutoRestart");

                if (!value)
                {
                    ResetAutoRestartState();
                }
            }
        }
    }

    public IReadOnlyList<string> Profiles { get; } = ["Standard", "MirrorOnly", "Audio", "LowLatency", "Presentation"];

    public string TcpEndpoint => $"{TcpHost}:{TcpPort}";
    public string DeviceCountText => Devices.Count == 1 ? "1 device" : $"{Devices.Count} devices";
    public string TcpStatusText => Devices.Any(d => string.Equals(d, TcpEndpoint, StringComparison.OrdinalIgnoreCase)) ? "Connected" : "Disconnected";
    public string OverallStatusText => _scrcpyService.IsRunning() ? "RUNNING" : "READY";

    public IAsyncRelayCommand RefreshDevicesCommand { get; }
    public IAsyncRelayCommand AutoDetectEndpointCommand { get; }
    public IAsyncRelayCommand ConnectTcpCommand { get; }
    public IAsyncRelayCommand ConnectTcpAndStartCommand { get; }
    public IAsyncRelayCommand DisconnectTcpCommand { get; }
    public IAsyncRelayCommand StartUsbCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand RestartCommand { get; }
    public IRelayCommand CopyLogCommand { get; }

    public AndroidViewModel(IAdbService adbService, IScrcpyService scrcpyService, ISettingsService settingsService, ILoggingService loggingService)
    {
        _adbService = adbService;
        _scrcpyService = scrcpyService;
        _settingsService = settingsService;
        _loggingService = loggingService;

        TcpHost = _settingsService.Current.AndroidTcpHost;
        TcpPort = _settingsService.Current.AndroidTcpPort;
        AutoStartOnAppStart = _settingsService.Current.AutoStartAndroidService;
        AutoRestartOnFailure = _settingsService.Current.AutoRestartAndroidService;

        RefreshDevicesCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RefreshDevicesAsync, "Android.RefreshDevices"));
        AutoDetectEndpointCommand = new AsyncRelayCommand(() => SafeExecuteAsync(() => AutoDetectEndpointAsync(), "Android.AutoDetectEndpoint"));
        ConnectTcpCommand = new AsyncRelayCommand(() => SafeExecuteAsync(ConnectTcpAsync, "Android.ConnectTcp"));
        ConnectTcpAndStartCommand = new AsyncRelayCommand(() => SafeExecuteAsync(ConnectTcpAndStartAsync, "Android.ConnectAndStart"));
        DisconnectTcpCommand = new AsyncRelayCommand(() => SafeExecuteAsync(DisconnectTcpAsync, "Android.DisconnectTcp"));
        StartUsbCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StartUsbAsync, "Android.StartUsb"));
        StopCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StopAsync, "Android.Stop"));
        RestartCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartAsync, "Android.Restart"));
        CopyLogCommand = new RelayCommand(CopyLogToClipboard);

        _scrcpyService.LogReceived += OnScrcpyLogReceived;
        _ = SafeExecuteAsync(RefreshDevicesAsync, "Android.InitialRefresh");
    }

    public async Task RefreshDevicesAsync()
    {
        try
        {
            await AutoDetectEndpointAsync(refreshDevices: false);
            await RefreshDevicesListAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: Device refresh failed: {ex.Message}");
        }
    }

    private async Task AutoDetectEndpointAsync(bool refreshDevices = true)
    {
        try
        {
            if (!await _adbService.IsAvailableAsync())
            {
                AppendLog("Auto-Erkennung: adb.exe nicht gefunden.");
                return;
            }

            var result = await _adbService.DiscoverTcpEndpointAsync(TcpPort);
            if (!result.Success)
            {
                AppendLog($"Auto-Erkennung: {result.Details}");
                return;
            }

            if (!TrySanitizeEndpoint(result.Host, result.Port, out var sanitizedHost, out var sanitizedPort))
            {
                AppendLog("Auto-Erkennung: Ungültiges Endpoint-Ergebnis verworfen.");
                return;
            }

            var hostChanged = !string.Equals(TcpHost, sanitizedHost, StringComparison.OrdinalIgnoreCase);
            var portChanged = TcpPort != sanitizedPort;

            TcpHost = sanitizedHost;
            TcpPort = sanitizedPort;

            if (hostChanged || portChanged)
            {
                AppendLog($"Auto-Erkennung ({result.Source}): {TcpHost}:{TcpPort}");
            }
            else
            {
                AppendLog($"Auto-Erkennung ({result.Source}): Endpoint unverändert ({TcpHost}:{TcpPort}).");
            }

            if (refreshDevices)
            {
                await RefreshDevicesListAsync();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: Auto-Erkennung fehlgeschlagen: {ex.Message}");
        }
    }

    private async Task RefreshDevicesListAsync()
    {
        Devices = await _adbService.GetConnectedDevicesAsync();
        AppendLog($"ADB devices found: {Devices.Count}");
        OnPropertyChanged(nameof(OverallStatusText));
    }

    private async Task ConnectTcpAsync()
    {
        try
        {
            Devices = await _adbService.GetConnectedDevicesAsync();
            var tcpSerial = TcpEndpoint;
            var hasUsbDevice = Devices.Any(d => !d.Contains(':'));

            if (hasUsbDevice)
            {
                await TryAutoDetectEndpointFromUsbAsync();
                tcpSerial = TcpEndpoint;
            }

            if (Devices.Any(d => string.Equals(d, tcpSerial, StringComparison.OrdinalIgnoreCase)))
            {
                AppendLog($"ADB TCP already connected: {tcpSerial}");
                return;
            }

            var connectResult = await _adbService.ConnectTcpWithOutputAsync(TcpHost, TcpPort);
            AppendLog(string.IsNullOrWhiteSpace(connectResult.Output)
                ? $"adb connect {TcpHost}:{TcpPort} returned no output."
                : $"adb connect output: {connectResult.Output}");

            if (!connectResult.Success && hasUsbDevice)
            {
                var tcpipOutput = await _adbService.EnableTcpIpModeAsync(TcpPort);
                AppendLog($"adb tcpip {TcpPort}: {tcpipOutput}");
                await Task.Delay(700);

                connectResult = await _adbService.ConnectTcpWithOutputAsync(TcpHost, TcpPort);
                AppendLog(string.IsNullOrWhiteSpace(connectResult.Output)
                    ? $"adb connect retry {TcpHost}:{TcpPort} returned no output."
                    : $"adb connect retry output: {connectResult.Output}");
            }

            AppendLog(connectResult.Success
                ? $"ADB TCP connected: {TcpHost}:{TcpPort}"
                : $"ADB TCP connect failed: {TcpHost}:{TcpPort}");

            if (connectResult.Success)
            {
                await RefreshDevicesAsync();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: TCP connect failed: {ex.Message}");
        }
    }

    private async Task ConnectTcpAndStartAsync()
    {
        _manualStopRequested = false;
        ResetAutoRestartState();
        await ConnectTcpAsync();

        try
        {
            if (!_settingsService.Current.EnableAndroidService)
            {
                AppendLog("Android-Modul ist in den Einstellungen deaktiviert.");
                return;
            }

            if (_settingsService.Current.RunMirroringInBackgroundOnly)
            {
                AppendLog("Hinweis: scrcpy läuft im Hintergrundmodus. Kein Mirroring-Fenster sichtbar. Deaktivierbar unter Settings -> Run UxPlay/scrcpy in background only.");
            }

            var scrcpyExe = _scrcpyService.GetExecutablePath();
            if (!File.Exists(scrcpyExe))
            {
                AppendLog($"ERR: scrcpy.exe not found: {scrcpyExe}");
                return;
            }

            var tcpSerial = $"{TcpHost}:{TcpPort}";
            var hasTcpDevice = Devices.Any(d => string.Equals(d, tcpSerial, StringComparison.OrdinalIgnoreCase));
            if (!hasTcpDevice)
            {
                AppendLog($"ERR: TCP device {tcpSerial} is not listed in adb devices. Mirror start skipped.");
                return;
            }

            await _scrcpyService.StartAsync(BuildProfile(serial: tcpSerial), hiddenOverride: false);
            AppendLog($"scrcpy TCP start requested for {tcpSerial}.");
            OnPropertyChanged(nameof(OverallStatusText));
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: scrcpy TCP start failed: {ex.Message}");
        }
    }

    private async Task DisconnectTcpAsync()
    {
        try
        {
            var disconnected = await _adbService.DisconnectTcpAsync(TcpHost, TcpPort);
            AppendLog(disconnected
                ? $"ADB TCP disconnected: {TcpHost}:{TcpPort}"
                : $"ADB TCP disconnect failed: {TcpHost}:{TcpPort}");
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: TCP disconnect failed: {ex.Message}");
        }
    }

    private async Task StartUsbAsync()
    {
        _manualStopRequested = false;
        ResetAutoRestartState();

        if (!_settingsService.Current.EnableAndroidService)
        {
            AppendLog("Android-Modul ist in den Einstellungen deaktiviert.");
            return;
        }

        try
        {
            Devices = await _adbService.GetConnectedDevicesAsync();
            if (Devices.Count == 0)
            {
                AppendLog("Kein Android-Gerät erkannt. USB-Debugging aktivieren und 'Devices refresh' prüfen.");
                return;
            }

            // Bootstrap wireless setup early: when USB is present, read WLAN endpoint automatically.
            await TryAutoDetectEndpointFromUsbAsync();

            var scrcpyExe = _scrcpyService.GetExecutablePath();
            if (!File.Exists(scrcpyExe))
            {
                AppendLog($"ERR: scrcpy.exe not found: {scrcpyExe}");
                return;
            }

            AppendLog($"Using scrcpy executable: {scrcpyExe}");

            await _scrcpyService.StartAsync(BuildProfile(selectUsb: true), hiddenOverride: false);
            AppendLog("scrcpy start requested.");
            OnPropertyChanged(nameof(OverallStatusText));
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: scrcpy start failed: {ex.Message}");
        }
    }

    private async Task TryAutoDetectEndpointFromUsbAsync()
    {
        try
        {
            var result = await _adbService.DiscoverTcpEndpointAsync(TcpPort);
            if (!result.Success)
            {
                return;
            }

            // During USB bootstrap we only auto-apply endpoints that are confidently USB-derived
            // or already connected via TCP device listing.
            if (!string.Equals(result.Source, "usb", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(result.Source, "connected", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!TrySanitizeEndpoint(result.Host, result.Port, out var sanitizedHost, out var sanitizedPort))
            {
                return;
            }

            var hostChanged = !string.Equals(TcpHost, sanitizedHost, StringComparison.OrdinalIgnoreCase);
            var portChanged = TcpPort != sanitizedPort;
            if (!hostChanged && !portChanged)
            {
                return;
            }

            TcpHost = sanitizedHost;
            TcpPort = sanitizedPort;
            AppendLog($"USB Auto-Erkennung: {TcpHost}:{TcpPort}");
        }
        catch (Exception ex)
        {
            // Keep silent to the user-facing flow except log line; this must never interrupt USB start.
            AppendLog($"Hinweis: USB Auto-Erkennung übersprungen ({ex.Message}).");
        }
    }

    private static bool TrySanitizeEndpoint(string? host, int port, out string sanitizedHost, out int sanitizedPort)
    {
        sanitizedHost = host?.Trim() ?? string.Empty;
        sanitizedPort = Math.Clamp(port, 1, 65535);
        return !string.IsNullOrWhiteSpace(sanitizedHost);
    }

    private async Task StopAsync()
    {
        _manualStopRequested = true;
        ResetAutoRestartState();

        try
        {
            await _scrcpyService.StopAsync();
            AppendLog("scrcpy stop requested.");
            OnPropertyChanged(nameof(OverallStatusText));
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: scrcpy stop failed: {ex.Message}");
        }
    }

    private async Task RestartAsync()
    {
        _manualStopRequested = false;
        ResetAutoRestartState();

        try
        {
            await _scrcpyService.RestartAsync(BuildProfile(selectUsb: true), hiddenOverride: false);
            AppendLog("scrcpy restart requested.");
            OnPropertyChanged(nameof(OverallStatusText));
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: scrcpy restart failed: {ex.Message}");
        }
    }

    private void OnScrcpyLogReceived(object? sender, string line)
    {
        try
        {
            AppendLog(line);

            if (ShouldTriggerAutoRestart(line))
            {
                _ = TriggerAutoRestartAsync(line);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("scrcpy log handler failed", ex, "Android.LogHandler");
        }
    }

    private bool ShouldTriggerAutoRestart(string line)
    {
        if (!AutoRestartOnFailure || _manualStopRequested)
        {
            return false;
        }

        if (line.Contains("exited (code", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (line.Contains("disconnected", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("connection lost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return line.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase) && !_scrcpyService.IsRunning();
    }

    private async Task TriggerAutoRestartAsync(string reason)
    {
        if (!await _autoRestartGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            if (_autoRestartAttempts >= MaxAutoRestartAttempts)
            {
                if (!_autoRestartLimitReachedLogged)
                {
                    _autoRestartLimitReachedLogged = true;
                    AppendLog($"Auto-Restart deaktiviert: Maximum von {MaxAutoRestartAttempts} Versuchen erreicht.");
                }

                return;
            }

            if (!_settingsService.Current.EnableAndroidService)
            {
                return;
            }

            _manualStopRequested = false;
            _autoRestartAttempts++;
            AppendLog($"Auto-Restart {_autoRestartAttempts}/{MaxAutoRestartAttempts}: Android-Restart in {AutoRestartDelay.TotalSeconds:0}s ({reason}).");
            await Task.Delay(AutoRestartDelay);
            await RestartBestEffortAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: Auto-Restart Android fehlgeschlagen: {ex.Message}");
            _loggingService.LogError("Android auto-restart failed", ex, "Android.AutoRestart");
        }
        finally
        {
            _autoRestartGate.Release();
        }
    }

    private void ResetAutoRestartState()
    {
        _autoRestartAttempts = 0;
        _autoRestartLimitReachedLogged = false;
    }

    private async Task RestartBestEffortAsync()
    {
        await RefreshDevicesAsync();

        var tcpSerial = TcpEndpoint;
        var hasTcpDevice = Devices.Any(d => string.Equals(d, tcpSerial, StringComparison.OrdinalIgnoreCase));
        if (!hasTcpDevice)
        {
            await ConnectTcpAsync();
            await RefreshDevicesAsync();
            hasTcpDevice = Devices.Any(d => string.Equals(d, tcpSerial, StringComparison.OrdinalIgnoreCase));
        }

        if (hasTcpDevice)
        {
            await _scrcpyService.RestartAsync(BuildProfile(serial: tcpSerial), hiddenOverride: false);
            OnPropertyChanged(nameof(OverallStatusText));
            return;
        }

        var hasUsbDevice = Devices.Any(d => !d.Contains(':'));
        if (hasUsbDevice)
        {
            await _scrcpyService.RestartAsync(BuildProfile(selectUsb: true), hiddenOverride: false);
            OnPropertyChanged(nameof(OverallStatusText));
            return;
        }

        AppendLog("Auto-Restart: Kein verbundenes Android-Gerät für Recovery gefunden.");
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        void UpdateLog()
        {
            var updated = string.Concat(LiveLog, Environment.NewLine, line);
            if (updated.Length > MaxLiveLogLength)
            {
                updated = updated.Substring(updated.Length - MaxLiveLogLength);
            }

            LiveLog = updated;
        }

        DispatcherQueue? queue = App.UiDispatcherQueue;
        if (queue is null || queue.HasThreadAccess)
        {
            UpdateLog();
            return;
        }

        queue.TryEnqueue(UpdateLog);
    }

    private void CopyLogToClipboard()
    {
        try
        {
            var package = new DataPackage();
            package.SetText(LiveLog ?? string.Empty);
            Clipboard.SetContent(package);
            AppendLog("Log copied to clipboard.");
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: Log copy failed: {ex.Message}");
        }
    }

    private ScrcpyProfile BuildProfile(bool selectUsb = false, bool selectTcpIp = false, string? serial = null)
    {
        return SelectedProfile switch
        {
            "MirrorOnly" => new ScrcpyProfile { Name = "MirrorOnly", DeviceSerial = serial, MirrorOnly = true, EnableAudio = false, SelectUsb = selectUsb, SelectTcpIp = selectTcpIp },
            "Audio" => new ScrcpyProfile { Name = "Audio", DeviceSerial = serial, EnableAudio = true, SelectUsb = selectUsb, SelectTcpIp = selectTcpIp },
            "LowLatency" => new ScrcpyProfile { Name = "LowLatency", DeviceSerial = serial, EnableAudio = false, LowLatency = true, SelectUsb = selectUsb, SelectTcpIp = selectTcpIp },
            "Presentation" => new ScrcpyProfile { Name = "Presentation", DeviceSerial = serial, EnableAudio = false, PresentationMode = true, SelectUsb = selectUsb, SelectTcpIp = selectTcpIp },
            _ => new ScrcpyProfile { Name = "Standard", DeviceSerial = serial, EnableAudio = false, SelectUsb = selectUsb, SelectTcpIp = selectTcpIp }
        };
    }

    private async Task SafeExecuteAsync(Func<Task> action, string operation)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog("ERR: Aktion fehlgeschlagen, App bleibt aktiv.");
            _loggingService.LogError($"{operation} failed", ex, "Android");
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
}
