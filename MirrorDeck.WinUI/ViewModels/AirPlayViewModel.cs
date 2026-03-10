using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using MirrorDeck.WinUI.Services.Interfaces;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;

namespace MirrorDeck.WinUI.ViewModels;

public class AirPlayViewModel : ObservableObject
{
    private const int MaxLiveLogLength = 32000;
    private const int MaxAutoRestartAttempts = 10;
    private static readonly TimeSpan AutoRestartDelay = TimeSpan.FromSeconds(5);

    private readonly IUxPlayService _uxPlayService;
    private readonly IBonjourService _bonjourService;
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;
    private readonly SemaphoreSlim _autoRestartGate = new(1, 1);

    private string _liveLog = "UxPlay logs appear here...";
    private bool _bonjourInstalled;
    private bool _bonjourRunning;
    private string _airPlayName = string.Empty;
    private bool _autoStartOnAppStart;
    private bool _autoRestartOnFailure;
    private bool _requirePinOnFirstConnect;
    private string _fixedPin = string.Empty;
    private bool _manualStopRequested;
    private int _autoRestartAttempts;
    private bool _autoRestartLimitReachedLogged;

    public string LiveLog
    {
        get => _liveLog;
        set => SetProperty(ref _liveLog, value);
    }

    public bool BonjourInstalled
    {
        get => _bonjourInstalled;
        set
        {
            if (SetProperty(ref _bonjourInstalled, value))
            {
                OnPropertyChanged(nameof(BonjourInstalledText));
            }
        }
    }

    public bool BonjourRunning
    {
        get => _bonjourRunning;
        set
        {
            if (SetProperty(ref _bonjourRunning, value))
            {
                OnPropertyChanged(nameof(BonjourRunningText));
                OnPropertyChanged(nameof(OverallStatusText));
            }
        }
    }

    public string AirPlayName
    {
        get => _airPlayName;
        set
        {
            if (SetProperty(ref _airPlayName, value))
            {
                _settingsService.Current.AirPlayName = value;
                SaveSettingsFireAndForget("AirPlay.Name");
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
                _settingsService.Current.AutoStartUxPlay = value;
                SaveSettingsFireAndForget("AirPlay.AutoStart");
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
                _settingsService.Current.AutoRestartUxPlay = value;
                SaveSettingsFireAndForget("AirPlay.AutoRestart");

                if (!value)
                {
                    ResetAutoRestartState();
                }
            }
        }
    }

    public bool RequirePinOnFirstConnect
    {
        get => _requirePinOnFirstConnect;
        set
        {
            if (SetProperty(ref _requirePinOnFirstConnect, value))
            {
                _settingsService.Current.AirPlayRequirePinOnFirstConnect = value;
                SaveSettingsFireAndForget("AirPlay.RequirePinOnFirstConnect");
            }
        }
    }

    public string FixedPin
    {
        get => _fixedPin;
        set
        {
            var normalized = NormalizePin(value);
            if (SetProperty(ref _fixedPin, normalized))
            {
                _settingsService.Current.AirPlayFixedPin = normalized;
                OnPropertyChanged(nameof(PinModeHintText));
                SaveSettingsFireAndForget("AirPlay.FixedPin");
            }
        }
    }

    public string PinModeHintText => string.IsNullOrWhiteSpace(FixedPin)
        ? "Leer lassen = zufaellige PIN bei erster Verbindung"
        : "Gespeichert: feste 4-stellige PIN";

    public string BonjourInstalledText => BonjourInstalled ? "Installed" : "Missing";
    public string BonjourRunningText => BonjourRunning ? "Running" : "Stopped";
    public string OverallStatusText => BonjourRunning ? "RUNNING" : (BonjourInstalled ? "READY" : "SETUP NEEDED");

    public IAsyncRelayCommand StartCommand { get; }
    public IAsyncRelayCommand StopCommand { get; }
    public IAsyncRelayCommand RestartCommand { get; }
    public IAsyncRelayCommand RefreshBonjourStatusCommand { get; }
    public IAsyncRelayCommand RestartBonjourCommand { get; }
    public IRelayCommand CopyLogCommand { get; }

    public AirPlayViewModel(IUxPlayService uxPlayService, IBonjourService bonjourService, ISettingsService settingsService, ILoggingService loggingService)
    {
        _uxPlayService = uxPlayService;
        _bonjourService = bonjourService;
        _settingsService = settingsService;
        _loggingService = loggingService;

        StartCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StartAsync, "AirPlay.Start"));
        StopCommand = new AsyncRelayCommand(() => SafeExecuteAsync(StopAsync, "AirPlay.Stop"));
        RestartCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartAsync, "AirPlay.Restart"));
        RefreshBonjourStatusCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RefreshBonjourStatusAsync, "AirPlay.RefreshBonjour"));
        RestartBonjourCommand = new AsyncRelayCommand(() => SafeExecuteAsync(RestartBonjourAsync, "AirPlay.RestartBonjour"));
        CopyLogCommand = new RelayCommand(CopyLogToClipboard);

        AirPlayName = _settingsService.Current.AirPlayName;
        AutoStartOnAppStart = _settingsService.Current.AutoStartUxPlay;
        AutoRestartOnFailure = _settingsService.Current.AutoRestartUxPlay;
        RequirePinOnFirstConnect = _settingsService.Current.AirPlayRequirePinOnFirstConnect;
        FixedPin = _settingsService.Current.AirPlayFixedPin;

        _uxPlayService.LogReceived += OnUxPlayLogReceived;
        _ = SafeExecuteAsync(RefreshBonjourStatusAsync, "AirPlay.InitialRefresh");
    }

    private Task StartAsync()
    {
        _manualStopRequested = false;
        ResetAutoRestartState();

        if (RequirePinOnFirstConnect && !string.IsNullOrWhiteSpace(FixedPin) && FixedPin.Length != 4)
        {
            AppendLog("ERR: PIN muss genau 4-stellig sein oder leer bleiben.");
            return Task.CompletedTask;
        }

        if (!_settingsService.Current.EnableAirPlayService)
        {
            AppendLog("AirPlay ist in den Einstellungen deaktiviert.");
            return Task.CompletedTask;
        }

        if (_settingsService.Current.RunMirroringInBackgroundOnly)
        {
            AppendLog("Hinweis: UxPlay läuft im Hintergrundmodus. Kein Receiver-Fenster sichtbar. Deaktivierbar unter Settings -> Run UxPlay/scrcpy in background only.");
        }

        return StartAndRefreshAsync();
    }

    private async Task StartAndRefreshAsync()
    {
        if (!await EnsureBonjourReadyForAirPlayAsync())
        {
            await RefreshBonjourStatusAsync();
            return;
        }

        if (_uxPlayService.IsRunning())
        {
            await _uxPlayService.RestartAsync();
            AppendLog("UxPlay war bereits aktiv und wurde neu gestartet.");
        }
        else
        {
            await _uxPlayService.StartAsync();
        }

        await RefreshBonjourStatusAsync();
    }

    private async Task<bool> EnsureBonjourReadyForAirPlayAsync()
    {
        var installed = await _bonjourService.IsInstalledAsync();
        if (!installed)
        {
            AppendLog("Bonjour fehlt. Versuche automatische Installation (Admin)...");
            var installerPath = await _bonjourService.DownloadInstallerAsync();
            if (string.IsNullOrWhiteSpace(installerPath))
            {
                AppendLog("ERR: Bonjour-Installer konnte nicht heruntergeladen werden.");
                return false;
            }

            installed = await _bonjourService.InstallDownloadedInstallerAsync(installerPath);
            if (!installed)
            {
                AppendLog("ERR: Bonjour konnte nicht installiert werden (UAC abgebrochen oder Installationsfehler).");
                return false;
            }

            AppendLog("Bonjour wurde installiert.");
        }

        if (!await _bonjourService.IsRunningAsync())
        {
            var started = await _bonjourService.StartAsync();
            if (!started)
            {
                AppendLog("WARN: Bonjour-Dienst startete nicht. Versuche Reparaturinstallation...");
                var installerPath = await _bonjourService.DownloadInstallerAsync();
                if (!string.IsNullOrWhiteSpace(installerPath))
                {
                    _ = await _bonjourService.InstallDownloadedInstallerAsync(installerPath);
                    started = await _bonjourService.StartAsync();
                }
            }

            if (!started)
            {
                AppendLog("ERR: Bonjour-Dienst konnte nicht gestartet werden. AirPlay-Start wird abgebrochen.");
                return false;
            }

            AppendLog("Bonjour-Dienst wurde gestartet.");
            await Task.Delay(500);
        }

        return true;
    }

    private async Task StopAsync()
    {
        _manualStopRequested = true;
        ResetAutoRestartState();
        await _uxPlayService.StopAsync();
        await RefreshBonjourStatusAsync();
    }

    private async Task RestartAsync()
    {
        _manualStopRequested = false;
        ResetAutoRestartState();
        await _uxPlayService.RestartAsync();
        await RefreshBonjourStatusAsync();
    }

    private async Task RestartBonjourAsync()
    {
        await _bonjourService.RestartAsync();
        await RefreshBonjourStatusAsync();
    }

    public async Task RefreshBonjourStatusAsync()
    {
        BonjourInstalled = await _bonjourService.IsInstalledAsync();
        BonjourRunning = await _bonjourService.IsRunningAsync();
    }

    private void OnUxPlayLogReceived(object? sender, string line)
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
            _loggingService.LogError("UxPlay log handler failed", ex, "AirPlay.LogHandler");
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

        return line.StartsWith("ERR:", StringComparison.OrdinalIgnoreCase) && !_uxPlayService.IsRunning();
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

            if (!_settingsService.Current.EnableAirPlayService)
            {
                return;
            }

            _manualStopRequested = false;
            _autoRestartAttempts++;
            AppendLog($"Auto-Restart { _autoRestartAttempts }/{MaxAutoRestartAttempts}: UxPlay-Restart in {AutoRestartDelay.TotalSeconds:0}s ({reason}).");
            await Task.Delay(AutoRestartDelay);
            await StartAndRefreshAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"ERR: Auto-Restart AirPlay fehlgeschlagen: {ex.Message}");
            _loggingService.LogError("AirPlay auto-restart failed", ex, "AirPlay.AutoRestart");
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

    private async Task SafeExecuteAsync(Func<Task> action, string operation)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppendLog("ERR: Aktion fehlgeschlagen, App bleibt aktiv.");
            _loggingService.LogError($"{operation} failed", ex, "AirPlay");
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

    private static string NormalizePin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = Regex.Replace(value, "[^0-9]", string.Empty);
        if (digits.Length > 4)
        {
            digits = digits.Substring(0, 4);
        }

        return digits;
    }
}
