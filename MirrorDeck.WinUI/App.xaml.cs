using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Logging;
using MirrorDeck.WinUI.Services;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.Settings;
using MirrorDeck.WinUI.ViewModels;
using MirrorDeck.WinUI.Views;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace MirrorDeck.WinUI;

public partial class App : Application
{
    private static readonly Mutex SingleInstanceMutex = new(false, @"Local\MirrorDeck.WinUI.Singleton");
    private static bool _ownsSingleInstanceMutex;

    private Window? _window;
    private bool _isShuttingDown;
    private bool _shutdownCleanupCompleted;
    private bool _startupDetectedUncleanShutdown;
    private readonly SemaphoreSlim _shutdownGate = new(1, 1);
    public static Microsoft.UI.Dispatching.DispatcherQueue? UiDispatcherQueue { get; private set; }
    public MainWindow? MainWindowInstance => _window as MainWindow;

    public static IHost Host { get; } = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) =>
        {
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IProcessRunner, ProcessRunner.ProcessRunner>();
            services.AddSingleton<IUxPlayService, UxPlayService>();
            services.AddSingleton<IAdbService, AdbService>();
            services.AddSingleton<IScrcpyService, ScrcpyService>();
            services.AddSingleton<IBonjourService, BonjourService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IDependencyService, DependencyManagement.DependencyService>();
            services.AddSingleton<ITrayService, TrayService>();
            services.AddSingleton<IAutoStartService, AutoStartService>();
            services.AddSingleton<IUpdateService, UpdateService>();

            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<AirPlayViewModel>();
            services.AddSingleton<AndroidViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<LogsViewModel>();
            services.AddSingleton<SetupAssistantViewModel>();
            services.AddSingleton<HelpViewModel>();

            services.AddSingleton<MainWindow>();
            services.AddTransient<FirstRunModuleWindow>();
            services.AddTransient<LogsWindow>();
            services.AddTransient<HelpWindow>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<AirPlayPage>();
            services.AddTransient<AndroidPage>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<LogsPage>();
            services.AddTransient<SetupAssistantPage>();
            services.AddTransient<HelpPage>();
            services.AddTransient<FirstRunSetupPage>();
        })
        .Build();

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!TryAcquireSingleInstance())
        {
            WriteStartupTrace("OnLaunched: second instance detected; activating existing window");
            TryActivateExistingInstanceWindow();
            Current.Exit();
            return;
        }

        _startupDetectedUncleanShutdown = TryMarkCurrentSessionAsActive();

        MainWindow? mainWindow = null;
        WriteStartupTrace("OnLaunched: begin");

        try
        {
            WriteStartupTrace("OnLaunched: resolving main window");
            _window = Host.Services.GetRequiredService<MainWindow>();
            _window.Closed += OnMainWindowClosed;
            mainWindow = (MainWindow)_window;
            _window.Activate();
            UiDispatcherQueue = _window.DispatcherQueue;

            WriteStartupTrace("OnLaunched: showing in-window startup overlay");
            await mainWindow.ShowStartupOverlayAsync();
            await mainWindow.SetStartupProgressAsync(12, "Initialisierung der Dienste...");

            WriteStartupTrace("OnLaunched: Host.StartAsync begin");
            await Host.StartAsync();
            WriteStartupTrace("OnLaunched: Host.StartAsync done");

            if (_startupDetectedUncleanShutdown)
            {
                await mainWindow.SetStartupProgressAsync(24, "Vorheriger Lauf war nicht sauber. Recovery läuft...");
                await RecoverFromUncleanShutdownAsync();
            }

            await mainWindow.SetStartupProgressAsync(38, "Konfiguration wird geladen...");

            var settingsService = Host.Services.GetRequiredService<ISettingsService>();
            WriteStartupTrace("OnLaunched: settings load begin");
            await settingsService.LoadAsync();
            WriteStartupTrace("OnLaunched: settings load done");
            ThemeCoordinator.ApplyTheme(settingsService.Current.Theme);
            await mainWindow.SetStartupProgressAsync(62, "Oberfläche wird vorbereitet...");
            mainWindow.ApplyInitialNavigation();

            var trayService = Host.Services.GetRequiredService<ITrayService>();
            WriteStartupTrace("OnLaunched: initializing tray");

            try
            {
                trayService.Initialize(_window);
                WriteStartupTrace("OnLaunched: tray initialized");
            }
            catch (Exception ex)
            {
                var logger = Host.Services.GetRequiredService<ILoggingService>();
                logger.LogError("Tray initialization failed, continuing without tray", ex, "AppLifecycle");
                WriteStartupTrace("OnLaunched: tray init failed: " + ex.Message);
            }

            if (settingsService.Current.StartMinimized && trayService.IsReady)
            {
                trayService.HideMainWindow();
            }

            _ = Task.Run(() => CheckForMirrorDeckUpdatesAsync());

            await mainWindow.SetStartupProgressAsync(84, "Module und Hintergrunddienste werden geprüft...");

            if (settingsService.Current.HasCompletedInitialModuleSetup)
            {
                _ = Task.Run(() => AutoStartConfiguredModulesAsync());
            }
            else
            {
                WriteStartupTrace("OnLaunched: module autostart deferred until first-run selection is confirmed");
            }

            await mainWindow.SetStartupProgressAsync(100, "MirrorDeck ist bereit.", delayMs: 160);
            await mainWindow.ShowMainContentAsync();

            WriteStartupTrace("OnLaunched: complete");
        }
        catch (Exception ex)
        {
            WriteStartupTrace("OnLaunched: exception: " + ex);
            try
            {
                var logger = Host.Services.GetRequiredService<ILoggingService>();
                logger.LogError("Application launch failed", ex, "AppLifecycle");
            }
            catch
            {
                // Logging is best-effort in startup failure path.
            }

            try
            {
                // Keep app usable even if non-critical launch path failed.
                if (_window is null)
                {
                    WriteStartupTrace("OnLaunched: fallback main window begin");
                    _window = Host.Services.GetRequiredService<MainWindow>();
                    _window.Closed += OnMainWindowClosed;
                    mainWindow = (MainWindow)_window;
                    _window.Activate();
                    UiDispatcherQueue = _window.DispatcherQueue;
                    WriteStartupTrace("OnLaunched: fallback main window activated");
                }

                if (mainWindow is not null)
                {
                    mainWindow.ApplyInitialNavigation();
                    await mainWindow.ShowMainContentAsync();
                }
            }
            catch
            {
                // If even fallback window creation fails, app will terminate naturally.
                WriteStartupTrace("OnLaunched: fallback main window failed");
            }
        }
    }

    public async Task RequestControlledShutdownAsync(string reason = "UserExit")
    {
        if (_isShuttingDown)
        {
            return;
        }

        _isShuttingDown = true;
        var mainWindow = _window as MainWindow;

        try
        {
            await RunOnUiThreadAsync(() =>
            {
                if (_window is MainWindow positionedMain)
                {
                    positionedMain.CloseAuxiliaryWindows();
                }
            });

            if (mainWindow is not null)
            {
                await mainWindow.ShowShutdownOverlayAsync();
                await mainWindow.SetShutdownProgressAsync(100, "Beende aktive Verbindungen...", desaturationLevel: 0.12, delayMs: 220);
                await mainWindow.SetShutdownProgressAsync(76, "Dienste und Sessions werden gestoppt...", desaturationLevel: 0.34, delayMs: 250);
                await mainWindow.SetShutdownProgressAsync(48, "Ressourcen werden freigegeben...", desaturationLevel: 0.6, delayMs: 260);
                await mainWindow.SetShutdownProgressAsync(18, "Abschaltinitialisierung läuft...", desaturationLevel: 0.82, delayMs: 260);
            }

            await EnsureShutdownCleanupAsync($"ControlledShutdown:{reason}");

            if (mainWindow is not null)
            {
                await mainWindow.SetShutdownProgressAsync(0, "MirrorDeck wurde beendet.", desaturationLevel: 1.0, delayMs: 160);
                await mainWindow.HideShutdownOverlayAsync(320);
            }
        }
        catch (Exception ex)
        {
            try
            {
                var logger = Host.Services.GetRequiredService<ILoggingService>();
                logger.LogError("Controlled shutdown failed", ex, "AppLifecycle");
            }
            catch
            {
                // Best-effort logging only.
            }
        }
        finally
        {
            await RunOnUiThreadAsync(() =>
            {
                try
                {
                    if (_window is MainWindow mainWindow)
                    {
                        mainWindow.Closed -= OnMainWindowClosed;
                        mainWindow.ForceCloseFromTray();
                    }
                }
                catch
                {
                    // Best-effort window close.
                }

                try
                {
                    Current.Exit();
                }
                catch
                {
                    // Best-effort app exit.
                }
            });
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var logger = Host.Services.GetRequiredService<ILoggingService>();
        logger.LogError("Unhandled UI exception", e.Exception);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var logger = Host.Services.GetRequiredService<ILoggingService>();
            logger.LogError("Unobserved task exception", e.Exception, "AppLifecycle");
            e.SetObserved();
        }
        catch
        {
            e.SetObserved();
        }
    }

    private void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        try
        {
            var logger = Host.Services.GetRequiredService<ILoggingService>();
            if (e.ExceptionObject is Exception ex)
            {
                logger.LogError("Unhandled domain exception", ex, "AppLifecycle");
            }
            else
            {
                logger.LogError("Unhandled domain exception (non-Exception payload)", null, "AppLifecycle");
            }
        }
        catch
        {
            // Logging is best-effort during terminal exception handling.
        }
    }

    private async Task AutoStartConfiguredModulesAsync()
    {
        var logger = Host.Services.GetRequiredService<ILoggingService>();
        var settings = Host.Services.GetRequiredService<ISettingsService>().Current;
        var bonjourService = Host.Services.GetRequiredService<IBonjourService>();
        var uxPlayService = Host.Services.GetRequiredService<IUxPlayService>();
        var scrcpyService = Host.Services.GetRequiredService<IScrcpyService>();
        var adbService = Host.Services.GetRequiredService<IAdbService>();
        var trayService = Host.Services.GetRequiredService<ITrayService>();

        try
        {
            var uxInstalled = File.Exists(uxPlayService.GetExecutablePath());
            if (settings.EnableAirPlayService && settings.AutoStartUxPlay && uxInstalled && !uxPlayService.IsRunning())
            {
                if (!await bonjourService.IsInstalledAsync())
                {
                    logger.LogWarning("Skipping UxPlay autostart because Bonjour is not installed", "AppLifecycle");
                }
                else if (!await bonjourService.IsRunningAsync())
                {
                    var started = await bonjourService.StartAsync();
                    if (!started)
                    {
                        logger.LogWarning("Skipping UxPlay autostart because Bonjour service could not be started", "AppLifecycle");
                    }
                }

                if (await bonjourService.IsRunningAsync())
                {
                    await uxPlayService.StartAsync();
                }
            }

            var scrcpyInstalled = File.Exists(scrcpyService.GetExecutablePath());
            if (settings.EnableAndroidService && settings.AutoStartAndroidService && scrcpyInstalled && !scrcpyService.IsRunning())
            {
                var serial = $"{settings.AndroidTcpHost}:{settings.AndroidTcpPort}";
                var devices = await adbService.GetConnectedDevicesAsync();
                var tcpConnected = devices.Any(d => string.Equals(d, serial, StringComparison.OrdinalIgnoreCase));

                if (!tcpConnected)
                {
                    var connectResult = await adbService.ConnectTcpWithOutputAsync(settings.AndroidTcpHost, settings.AndroidTcpPort);
                    tcpConnected = connectResult.Success;
                }

                await scrcpyService.StartAsync(new ScrcpyProfile
                {
                    Name = settings.DefaultScrcpyProfile,
                    EnableAudio = false,
                    DeviceSerial = tcpConnected ? serial : null
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Auto-start of configured modules failed", ex, "AppLifecycle");
        }
        finally
        {
            try
            {
                await trayService.RefreshStatusAsync();
            }
            catch (Exception ex)
            {
                logger.LogError("Tray status refresh after auto-start failed", ex, "AppLifecycle");
            }
        }
    }

    private async Task CheckForMirrorDeckUpdatesAsync()
    {
        try
        {
            var updateService = Host.Services.GetRequiredService<IUpdateService>();
            var settings = Host.Services.GetRequiredService<ISettingsService>().Current;
            var trayService = Host.Services.GetRequiredService<ITrayService>();
            var logger = Host.Services.GetRequiredService<ILoggingService>();

            var update = await updateService.CheckForUpdateAsync();
            if (!update.IsUpdateAvailable)
            {
                return;
            }

            logger.LogInfo(
                $"MirrorDeck update available: current={update.CurrentVersion}, latest={update.LatestVersion}",
                "Update");

            if (settings.EnableWindowsNotifications)
            {
                trayService.ShowInfoNotification("Update verfugbar", $"Neue Version {update.LatestVersion} ist verfugbar.");
            }
        }
        catch (Exception ex)
        {
            try
            {
                var logger = Host.Services.GetRequiredService<ILoggingService>();
                logger.LogError("Startup update check failed", ex, "Update");
            }
            catch
            {
                // Best-effort logging only.
            }
        }
    }

    private async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        if (_shutdownCleanupCompleted)
        {
            return;
        }

        _isShuttingDown = true;

        await EnsureShutdownCleanupAsync("MainWindowClosed");
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            ProcessControlHelper.KillProcessesByName("uxplay");
            ProcessControlHelper.KillProcessesByName("scrcpy");
        }
        catch
        {
            // Final shutdown fallback must be best-effort only.
        }
    }

    private async Task RecoverFromUncleanShutdownAsync()
    {
        try
        {
            var logger = Host.Services.GetRequiredService<ILoggingService>();
            var uxPlayService = Host.Services.GetRequiredService<IUxPlayService>();
            var scrcpyService = Host.Services.GetRequiredService<IScrcpyService>();
            var adbService = Host.Services.GetRequiredService<IAdbService>();

            logger.LogWarning("Detected unclean previous shutdown. Running startup recovery cleanup.", "AppLifecycle");

            await uxPlayService.StopAsync();
            await scrcpyService.StopAsync();

            var killedUxPlay = ProcessControlHelper.KillProcessesByName("uxplay");
            var killedScrcpy = ProcessControlHelper.KillProcessesByName("scrcpy");
            var killedAdb = ProcessControlHelper.TryKillProcessByExecutablePath(adbService.GetAdbPath());

            logger.LogInfo(
                $"Startup recovery cleanup terminated leftovers: uxplay={killedUxPlay}, scrcpy={killedScrcpy}, adb={(killedAdb ? 1 : 0)}",
                "AppLifecycle");
        }
        catch (Exception ex)
        {
            try
            {
                var logger = Host.Services.GetRequiredService<ILoggingService>();
                logger.LogError("Startup recovery after unclean shutdown failed", ex, "AppLifecycle");
            }
            catch
            {
                // Recovery logging is best-effort.
            }
        }
    }

    private async Task EnsureShutdownCleanupAsync(string source)
    {
        await _shutdownGate.WaitAsync();

        try
        {
            if (_shutdownCleanupCompleted)
            {
                return;
            }

            var logger = Host.Services.GetRequiredService<ILoggingService>();

            try
            {
                var uxPlayService = Host.Services.GetRequiredService<IUxPlayService>();
                var scrcpyService = Host.Services.GetRequiredService<IScrcpyService>();
                var adbService = Host.Services.GetRequiredService<IAdbService>();
                var trayService = Host.Services.GetRequiredService<ITrayService>();

                await uxPlayService.StopAsync();
                await scrcpyService.StopAsync();

                var killedUxPlay = ProcessControlHelper.KillProcessesByName("uxplay");
                var killedScrcpy = ProcessControlHelper.KillProcessesByName("scrcpy");
                var killedAdb = ProcessControlHelper.TryKillProcessByExecutablePath(adbService.GetAdbPath());

                if (killedUxPlay > 0 || killedScrcpy > 0 || killedAdb)
                {
                    logger.LogInfo(
                        $"Shutdown cleanup terminated leftovers: uxplay={killedUxPlay}, scrcpy={killedScrcpy}, adb={(killedAdb ? 1 : 0)}",
                        "AppLifecycle");
                }

                trayService.Dispose();
                await Host.StopAsync();
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed while shutting down mirrored services ({source})", ex, "AppLifecycle");
            }

            _shutdownCleanupCompleted = true;
            TryClearSessionMarker();
        }
        finally
        {
            _shutdownGate.Release();
        }
    }

    private static bool TryMarkCurrentSessionAsActive()
    {
        try
        {
            AppPaths.EnsureDirectories();

            var hadExistingMarker = File.Exists(AppPaths.SessionMarkerFile);
            var payload = new
            {
                StartedUtc = DateTimeOffset.UtcNow,
                ProcessId = Environment.ProcessId,
                Version = typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown"
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.SessionMarkerFile, json);
            return hadExistingMarker;
        }
        catch
        {
            // Session marker is best-effort; startup must continue.
            return false;
        }
    }

    private static void TryClearSessionMarker()
    {
        try
        {
            if (File.Exists(AppPaths.SessionMarkerFile))
            {
                File.Delete(AppPaths.SessionMarkerFile);
            }
        }
        catch
        {
            // Marker cleanup must not block shutdown.
        }
    }

    private static Task RunOnUiThreadAsync(Action action)
    {
        var queue = UiDispatcherQueue;
        if (queue is null || queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enqueued = queue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!enqueued)
        {
            tcs.TrySetException(new InvalidOperationException("Failed to enqueue UI action."));
        }

        return tcs.Task;
    }

    private static void TryApplySharedWindowBounds(AppWindow appWindow, Windows.Graphics.PointInt32 position)
    {
        try
        {
            appWindow.Move(position);
        }
        catch
        {
            // Window bounds synchronization is best-effort.
        }
    }

    private static void WriteStartupTrace(string message)
    {
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MirrorDeck");
            Directory.CreateDirectory(root);

            var path = Path.Combine(root, "startup-trace.log");
            var line = $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(line);

            using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            fs.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // Startup tracing must never break app startup.
        }
    }

    private static bool TryAcquireSingleInstance()
    {
        if (_ownsSingleInstanceMutex)
        {
            return true;
        }

        try
        {
            _ownsSingleInstanceMutex = SingleInstanceMutex.WaitOne(0, false);
            return _ownsSingleInstanceMutex;
        }
        catch (AbandonedMutexException)
        {
            _ownsSingleInstanceMutex = true;
            return true;
        }
    }

    private static void TryActivateExistingInstanceWindow()
    {
        try
        {
            var current = Process.GetCurrentProcess();
            var candidates = Process.GetProcessesByName(current.ProcessName)
                .Where(p => p.Id != current.Id && p.SessionId == current.SessionId)
                .OrderByDescending(p => p.StartTime)
                .ToList();

            foreach (var process in candidates)
            {
                try
                {
                    process.Refresh();
                    var hwnd = process.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (IsIconic(hwnd))
                    {
                        ShowWindow(hwnd, SwRestore);
                    }
                    else
                    {
                        ShowWindow(hwnd, SwShow);
                    }

                    SetForegroundWindow(hwnd);
                    break;
                }
                catch
                {
                    // Best-effort focus handoff.
                }
            }
        }
        catch
        {
            // Best-effort activation only.
        }
    }

    private const int SwRestore = 9;
    private const int SwShow = 5;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);
}
