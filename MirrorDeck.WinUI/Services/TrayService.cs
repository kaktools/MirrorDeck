using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.Views;

namespace MirrorDeck.WinUI.Services;

public sealed class TrayService : ITrayService
{
    private const uint WmAppTray = 0x8001;
    private const uint WmCommand = 0x0111;
    private const uint WmHotkey = 0x0312;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmLButtonUp = 0x0202;

    private const uint NifMessage = 0x0001;
    private const uint NifIcon = 0x0002;
    private const uint NifTip = 0x0004;
    private const uint NifInfo = 0x0010;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;

    private const int HotkeySnapshotId = 21001;
    private const int HotkeyPauseResumeId = 21002;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly IUxPlayService _uxPlayService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IAdbService _adbService;
    private readonly IBonjourService _bonjourService;
    private readonly ISettingsService _settingsService;
    private readonly ILoggingService _loggingService;

    private Window? _mainWindow;
    private IntPtr _mainHwnd;
    private IntPtr _iconHandle;
    private IntPtr _baseIconHandle;
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _oldWndProc;
    private TrayMenuWindow? _trayMenuWindow;
    private bool _initialized;
    private bool _disposed;
    private System.Threading.Timer? _statusTimer;
    private bool? _lastBonjourOk;
    private bool? _lastAdbOk;
    private bool? _lastUxRunning;
    private bool? _lastScrcpyRunning;
    private string _lastStatusSummary = string.Empty;
    private DateTimeOffset _lastStatusLogAtUtc = DateTimeOffset.MinValue;
    private string _registeredSnapshotShortcut = string.Empty;
    private string _registeredPauseResumeShortcut = string.Empty;

    public bool IsReady => _initialized && !_disposed && _mainHwnd != IntPtr.Zero;

    private enum MenuId : uint
    {
        ToggleWindow = 1100,
        AirStart = 1101,
        AirStop = 1102,
        AirRestart = 1103,

        AndroidUsbStart = 1200,
        AndroidTcpConnect = 1201,
        AndroidTcpDisconnect = 1202,
        AndroidStop = 1203,

        PauseResume = 1300,
        Snapshot = 1301,
        Help = 1302,
        Exit = 1500,
    }

    public TrayService(
        IUxPlayService uxPlayService,
        IScrcpyService scrcpyService,
        IAdbService adbService,
        IBonjourService bonjourService,
        ISettingsService settingsService,
        ILoggingService loggingService)
    {
        _uxPlayService = uxPlayService;
        _scrcpyService = scrcpyService;
        _adbService = adbService;
        _bonjourService = bonjourService;
        _settingsService = settingsService;
        _loggingService = loggingService;
    }

    public void Initialize(Window mainWindow)
    {
        if (_initialized)
        {
            return;
        }

        _mainWindow = mainWindow;
        _mainHwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
        if (_mainHwnd == IntPtr.Zero)
        {
            return;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MirrorDeck.ico");
        _baseIconHandle = LoadTrayBaseIcon(iconPath);
        _iconHandle = _baseIconHandle;

        _wndProcDelegate = SubclassWndProc;
        _oldWndProc = SetWindowLongPtr(_mainHwnd, -4, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        var data = BuildNotifyIconData(NifMessage | NifIcon | NifTip, "MirrorDeck wird initialisiert...");
        Shell_NotifyIcon(NimAdd, ref data);

        _statusTimer = new System.Threading.Timer(_ =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await RefreshStatusAsync();
                }
                catch (Exception ex)
                {
                    _loggingService.LogError("Tray timer refresh failed", ex, "Tray");
                }
            });
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(4));

        EnsureHotkeysRegistered();
        _initialized = true;
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        App.UiDispatcherQueue?.TryEnqueue(() =>
        {
            _mainWindow.AppWindow.Show();
            _mainWindow.Activate();
        });
    }

    public void HideMainWindow()
    {
        _mainWindow?.AppWindow.Hide();
    }

    public void ShowInfoNotification(string title, string message)
    {
        ShowBalloon(title, message);
    }

    private void ToggleMainWindowVisibility()
    {
        if (_mainWindow is null)
        {
            return;
        }

        App.UiDispatcherQueue?.TryEnqueue(() =>
        {
            if (_mainWindow.AppWindow.IsVisible)
            {
                _mainWindow.AppWindow.Hide();
                return;
            }

            _mainWindow.AppWindow.Show();
            _mainWindow.Activate();
        });
    }

    public async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        if (_mainHwnd == IntPtr.Zero || _disposed)
        {
            return;
        }

        EnsureHotkeysRegistered();

        var bonjourOk = await _bonjourService.IsRunningAsync(cancellationToken);
        var adbOk = await _adbService.IsAvailableAsync(cancellationToken);
        var uxRunning = _uxPlayService.IsRunning();
        var scrcpyRunning = _scrcpyService.IsRunning();

        UpdateTrayIcon();

        if (_lastBonjourOk.HasValue && _lastBonjourOk.Value != bonjourOk)
        {
            ShowBalloon("Bonjour status", bonjourOk ? "Bonjour is running again." : "Bonjour stopped. AirPlay discovery may fail.");
        }

        if (_lastAdbOk.HasValue && _lastAdbOk.Value != adbOk)
        {
            ShowBalloon("adb status", adbOk ? "adb is available." : "adb missing. Android features unavailable.");
        }

        if (_lastUxRunning.HasValue && _lastUxRunning.Value != uxRunning)
        {
            ShowBalloon("AirPlay session", uxRunning ? "UxPlay started." : "UxPlay stopped.");
        }

        if (_lastScrcpyRunning.HasValue && _lastScrcpyRunning.Value != scrcpyRunning)
        {
            ShowBalloon("Android session", scrcpyRunning ? "scrcpy started." : "scrcpy stopped.");
        }

        _lastBonjourOk = bonjourOk;
        _lastAdbOk = adbOk;
        _lastUxRunning = uxRunning;
        _lastScrcpyRunning = scrcpyRunning;

        var tooltip = $"MirrorDeck | UxPlay {(uxRunning ? "aktiv" : "inaktiv")} | scrcpy {(scrcpyRunning ? "aktiv" : "inaktiv")}";
        var data = BuildNotifyIconData(NifTip | NifIcon, tooltip);
        Shell_NotifyIcon(NimModify, ref data);

        var statusSummary = $"UxPlay={uxRunning}, scrcpy={scrcpyRunning}, Bonjour={bonjourOk}, adb={adbOk}";
        var nowUtc = DateTimeOffset.UtcNow;
        var statusChanged = !string.Equals(_lastStatusSummary, statusSummary, StringComparison.Ordinal);
        var heartbeatDue = (nowUtc - _lastStatusLogAtUtc) >= TimeSpan.FromMinutes(5);

        if (statusChanged || heartbeatDue)
        {
            _loggingService.LogInfo($"Tray status updated: {statusSummary}", "Tray");
            _lastStatusSummary = statusSummary;
            _lastStatusLogAtUtc = nowUtc;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _statusTimer?.Dispose();
        _statusTimer = null;

        if (_mainHwnd != IntPtr.Zero)
        {
            UnregisterGlobalHotkey(HotkeySnapshotId);
            UnregisterGlobalHotkey(HotkeyPauseResumeId);

            var data = BuildNotifyIconData(0, string.Empty);
            Shell_NotifyIcon(NimDelete, ref data);

            if (_oldWndProc != IntPtr.Zero)
            {
                _ = SetWindowLongPtr(_mainHwnd, -4, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
        }

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        if (_baseIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_baseIconHandle);
            _baseIconHandle = IntPtr.Zero;
        }

        _disposed = true;
    }

    private IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WmAppTray)
            {
                var eventCode = unchecked((uint)lParam.ToInt64());
                if (eventCode == WmLButtonUp)
                {
                    ToggleMainWindowVisibility();
                    return IntPtr.Zero;
                }

                if (eventCode == WmRButtonUp)
                {
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
            }
            else if (msg == WmCommand)
            {
                var menuId = (uint)(wParam.ToInt64() & 0xFFFF);
                _ = Task.Run(() => HandleMenuCommand(menuId));
                return IntPtr.Zero;
            }
            else if (msg == WmHotkey)
            {
                var hotkeyId = unchecked((int)wParam.ToInt64());
                _ = Task.Run(() => HandleHotkeyAsync(hotkeyId));
                return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Tray window procedure failed", ex, "Tray");
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        try
        {
            GetCursorPos(out var point);

            var uiQueue = App.UiDispatcherQueue;
            if (uiQueue is null)
            {
                return;
            }

            uiQueue.TryEnqueue(() => ShowContextMenuWindow(point));
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Tray context menu failed", ex, "Tray");
        }
    }

    private void ShowContextMenuWindow(POINT point)
    {
        try
        {
            _trayMenuWindow?.Close();
        }
        catch
        {
            // Best-effort close.
        }

        try
        {
            var menuWindow = new TrayMenuWindow();
            _trayMenuWindow = menuWindow;
            menuWindow.Closed += (_, _) =>
            {
                if (ReferenceEquals(_trayMenuWindow, menuWindow))
                {
                    _trayMenuWindow = null;
                }
            };

            menuWindow.Initialize(BuildMenuEntries(), async id => await HandleMenuCommand(id), new Windows.Graphics.PointInt32(point.X, point.Y));
            menuWindow.Activate();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Tray modern menu window failed", ex, "Tray");
        }
    }

    private IReadOnlyList<TrayMenuEntry> BuildMenuEntries()
    {
        var items = new List<TrayMenuEntry>();
        var uxRunning = _uxPlayService.IsRunning();
        var scrcpyRunning = _scrcpyService.IsRunning();
        var uxInstalled = uxRunning || File.Exists(_uxPlayService.GetExecutablePath());
        var scrcpyInstalled = File.Exists(_scrcpyService.GetExecutablePath());
        var airPlayEnabled = _settingsService.Current.EnableAirPlayService;
        var androidEnabled = _settingsService.Current.EnableAndroidService;
        var appVisible = _mainWindow?.AppWindow.IsVisible ?? true;

        if (airPlayEnabled || androidEnabled)
        {
            items.Add(new TrayMenuEntry(0, string.Empty, IsSeparator: true));
        }

        if (airPlayEnabled)
        {
            items.Add(new TrayMenuEntry((uint)MenuId.AirStart, "AirPlay Start", uxInstalled, IsActive: uxRunning, ActionKind: TrayActionKind.Start));
            items.Add(new TrayMenuEntry((uint)MenuId.AirStop, "AirPlay Stop", IsActive: uxRunning, ActionKind: TrayActionKind.Stop));
            items.Add(new TrayMenuEntry((uint)MenuId.AirRestart, "AirPlay Restart", IsActive: uxRunning, ActionKind: TrayActionKind.Restart));
        }

        if (airPlayEnabled && androidEnabled)
        {
            items.Add(new TrayMenuEntry(0, string.Empty, IsSeparator: true));
        }

        if (androidEnabled)
        {
            items.Add(new TrayMenuEntry((uint)MenuId.AndroidUsbStart, "Android Start", scrcpyInstalled, IsActive: scrcpyRunning, ActionKind: TrayActionKind.Start));
            items.Add(new TrayMenuEntry((uint)MenuId.AndroidTcpConnect, "Android Verbinden"));
            items.Add(new TrayMenuEntry((uint)MenuId.AndroidTcpDisconnect, "Android Trennen"));
            items.Add(new TrayMenuEntry((uint)MenuId.AndroidStop, "Android Stoppen", IsActive: scrcpyRunning, ActionKind: TrayActionKind.Stop));
        }

        items.Add(new TrayMenuEntry(0, string.Empty, IsSeparator: true));
        items.Add(new TrayMenuEntry((uint)MenuId.PauseResume, "Pause/Play", IsActive: uxRunning || scrcpyRunning));
        items.Add(new TrayMenuEntry((uint)MenuId.Snapshot, "Snapshots"));
        items.Add(new TrayMenuEntry(0, string.Empty, IsSeparator: true));
        items.Add(new TrayMenuEntry((uint)MenuId.Help, "Hilfe"));
        items.Add(new TrayMenuEntry((uint)MenuId.ToggleWindow, appVisible ? "App ausblenden" : "App einblenden", IsActive: appVisible));
        items.Add(new TrayMenuEntry((uint)MenuId.Exit, "Beenden"));

        return items;
    }

    private async Task HandleHotkeyAsync(int hotkeyId)
    {
        try
        {
            switch (hotkeyId)
            {
                case HotkeySnapshotId:
                    await CaptureSnapshotsAsync();
                    break;
                case HotkeyPauseResumeId:
                    await TogglePauseResumeAllAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Hotkey action failed: {hotkeyId}", ex, "Tray");
        }
    }

    private async Task CaptureSnapshotsAsync()
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
            ShowBalloon("Snapshot", "Kein aktives Fenster für Snapshot gefunden.");
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

        ShowBalloon("Snapshot", $"{snapshotFiles.Count} Fenster-Snapshot(s) erstellt.");
    }

    private async Task TogglePauseResumeAllAsync()
    {
        if (!_uxPlayService.IsRunning() && !_scrcpyService.IsRunning())
        {
            ShowBalloon("Pause/Play", "Keine aktive Session verfügbar.");
            return;
        }

        var pauseRequested = !_uxPlayService.IsPaused() && !_scrcpyService.IsPaused();

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

        ShowBalloon("Pause/Play", pauseRequested ? "Aktive Sessions pausiert." : "Aktive Sessions fortgesetzt.");
    }

    private void EnsureHotkeysRegistered()
    {
        if (_mainHwnd == IntPtr.Zero || _disposed)
        {
            return;
        }

        RegisterOrUpdateHotkey(
            _settingsService.Current.SnapshotShortcut,
            ref _registeredSnapshotShortcut,
            HotkeySnapshotId,
            "Snapshot");

        RegisterOrUpdateHotkey(
            _settingsService.Current.PauseResumeShortcut,
            ref _registeredPauseResumeShortcut,
            HotkeyPauseResumeId,
            "PauseResume");
    }

    private void RegisterOrUpdateHotkey(string configuredShortcut, ref string registeredShortcut, int hotkeyId, string label)
    {
        var trimmed = (configuredShortcut ?? string.Empty).Trim();
        if (string.Equals(trimmed, registeredShortcut, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        UnregisterGlobalHotkey(hotkeyId);
        registeredShortcut = string.Empty;

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        if (!TryParseHotkey(trimmed, out var modifiers, out var key))
        {
            _loggingService.LogWarning($"Invalid hotkey format for {label}: '{trimmed}'", "Tray");
            return;
        }

        if (!RegisterHotKey(_mainHwnd, hotkeyId, modifiers, key))
        {
            var lastError = Marshal.GetLastWin32Error();
            _loggingService.LogWarning(
                $"Hotkey for {label} is unavailable: '{trimmed}' (Win32={lastError}). Choose another shortcut in Setup.",
                "Tray");
            // Avoid repeating the same registration failure every status refresh.
            registeredShortcut = trimmed;
            return;
        }

        registeredShortcut = trimmed;
    }

    private void UnregisterGlobalHotkey(int hotkeyId)
    {
        if (_mainHwnd != IntPtr.Zero)
        {
            _ = UnregisterHotKey(_mainHwnd, hotkeyId);
        }
    }

    private static bool TryParseHotkey(string text, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        var tokens = text
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        if (tokens.Length == 0)
        {
            return false;
        }

        string? keyToken = null;
        foreach (var token in tokens)
        {
            if (token.Equals("CTRL", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("CONTROL", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("STRG", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
                continue;
            }

            if (token.Equals("SHIFT", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
                continue;
            }

            if (token.Equals("ALT", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
                continue;
            }

            if (token.Equals("WIN", StringComparison.OrdinalIgnoreCase) || token.Equals("WINDOWS", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
                continue;
            }

            keyToken = token;
        }

        if (string.IsNullOrWhiteSpace(keyToken))
        {
            return false;
        }

        if (keyToken.Length == 1)
        {
            var ch = char.ToUpperInvariant(keyToken[0]);
            if (ch >= 'A' && ch <= 'Z')
            {
                key = ch;
                return true;
            }

            if (ch >= '0' && ch <= '9')
            {
                key = ch;
                return true;
            }
        }

        if (keyToken.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(keyToken[1..], out var fn) && fn >= 1 && fn <= 24)
        {
            key = (uint)(0x70 + (fn - 1));
            return true;
        }

        key = keyToken.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "ENTER" or "RETURN" => 0x0D,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PGUP" or "PAGEUP" => 0x21,
            "PGDN" or "PAGEDOWN" => 0x22,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "PRINTSCREEN" => 0x2C,
            _ => 0
        };

        return key != 0;
    }

    private async Task HandleMenuCommand(uint rawMenuId)
    {
        var id = (MenuId)rawMenuId;

        try
        {
            switch (id)
            {
                case MenuId.ToggleWindow:
                    ToggleMainWindowVisibility();
                    break;
                case MenuId.AirStart:
                    if (_settingsService.Current.EnableAirPlayService)
                    {
                        await _uxPlayService.StartAsync();
                    }

                    break;
                case MenuId.AirStop:
                    await _uxPlayService.StopAsync();
                    break;
                case MenuId.AirRestart:
                    await _uxPlayService.RestartAsync();
                    break;
                case MenuId.PauseResume:
                    await TogglePauseResumeAllAsync();
                    break;
                case MenuId.Snapshot:
                    await CaptureSnapshotsAsync();
                    break;
                case MenuId.AndroidUsbStart:
                    if (_settingsService.Current.EnableAndroidService)
                    {
                        await _scrcpyService.StartAsync(new ScrcpyProfile { Name = "Standard", EnableAudio = false }, hiddenOverride: false);
                    }

                    break;
                case MenuId.AndroidTcpConnect:
                    await _adbService.ConnectTcpAsync(_settingsService.Current.AndroidTcpHost, _settingsService.Current.AndroidTcpPort);
                    break;
                case MenuId.AndroidTcpDisconnect:
                    await _adbService.DisconnectTcpAsync(_settingsService.Current.AndroidTcpHost, _settingsService.Current.AndroidTcpPort);
                    break;
                case MenuId.AndroidStop:
                    await _scrcpyService.StopAsync();
                    break;
                case MenuId.Help:
                    if (_mainWindow is MainWindow helpShell)
                    {
                        App.UiDispatcherQueue?.TryEnqueue(() =>
                        {
                            helpShell.NavigateTo("help");
                            ShowMainWindow();
                        });
                    }

                    break;
                case MenuId.Exit:
                    if (Application.Current is App app)
                    {
                        var uiQueue = App.UiDispatcherQueue;
                        if (uiQueue is not null && !uiQueue.HasThreadAccess)
                        {
                            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                            var enqueued = uiQueue.TryEnqueue(async () =>
                            {
                                try
                                {
                                    await app.RequestControlledShutdownAsync("TrayExit");
                                    tcs.TrySetResult();
                                }
                                catch (Exception ex)
                                {
                                    tcs.TrySetException(ex);
                                }
                            });

                            if (!enqueued)
                            {
                                throw new InvalidOperationException("Could not enqueue tray exit on UI thread.");
                            }

                            await tcs.Task;
                        }
                        else
                        {
                            await app.RequestControlledShutdownAsync("TrayExit");
                        }
                    }
                    else
                    {
                        Application.Current.Exit();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogError($"Tray action failed: {id}", ex, "Tray");
        }
        finally
        {
            try
            {
                await RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Tray status refresh after action failed", ex, "Tray");
            }
        }
    }

    private NOTIFYICONDATA BuildNotifyIconData(uint flags, string tip)
    {
        return new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _mainHwnd,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = WmAppTray,
            hIcon = _iconHandle,
            szTip = TrimTip(tip),
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };
    }

    private static string TrimTip(string tip)
    {
        if (string.IsNullOrWhiteSpace(tip))
        {
            return "MirrorDeck";
        }

        return tip.Length <= 127 ? tip : tip[..127];
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private void UpdateTrayIcon()
    {
        if (_baseIconHandle != IntPtr.Zero)
        {
            _iconHandle = _baseIconHandle;
        }
    }

    private static IntPtr LoadTrayBaseIcon(string iconPath)
    {
        const uint imageIcon = 1;
        const uint loadFromFile = 0x0010;
        const uint loadDefaultSize = 0x0040;

        if (File.Exists(iconPath))
        {
            var icon = LoadImage(IntPtr.Zero, iconPath, imageIcon, 0, 0, loadFromFile | loadDefaultSize);
            if (icon != IntPtr.Zero)
            {
                return icon;
            }
        }

        var exePath = Environment.ProcessPath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
        {
            _ = ExtractIconEx(exePath, 0, out var large, out var small, 1);
            if (small != IntPtr.Zero)
            {
                if (large != IntPtr.Zero)
                {
                    DestroyIcon(large);
                }

                return small;
            }

            if (large != IntPtr.Zero)
            {
                return large;
            }
        }

        return IntPtr.Zero;
    }

    private void ShowBalloon(string title, string message)
    {
        if (_mainHwnd == IntPtr.Zero || _disposed)
        {
            return;
        }

        if (!_settingsService.Current.EnableWindowsNotifications)
        {
            return;
        }

        var data = BuildNotifyIconData(NifInfo, "MirrorDeck");
        data.szInfoTitle = title;
        data.szInfo = message.Length <= 255 ? message : message[..255];
        data.dwInfoFlags = 0;
        Shell_NotifyIcon(NimModify, ref data);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
