using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Services;

public sealed class ScrcpyService : IScrcpyService
{
    private const string Key = "scrcpy";

    private readonly IProcessRunner _processRunner;
    private readonly ISettingsService _settingsService;
    private bool _isPaused;

    public event EventHandler<string>? LogReceived;

    public ScrcpyService(IProcessRunner processRunner, ISettingsService settingsService)
    {
        _processRunner = processRunner;
        _settingsService = settingsService;

        _processRunner.OutputReceived += OnOutputReceived;
        _processRunner.ErrorReceived += OnErrorReceived;
    }

    public async Task StartAsync(ScrcpyProfile profile, bool? hiddenOverride = null, CancellationToken cancellationToken = default)
    {
        var exe = GetExecutablePath();
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException("scrcpy executable not found", exe);
        }

        var args = BuildArguments(profile, _settingsService.Current);
        var hidden = hiddenOverride ?? false;
        LogReceived?.Invoke(this, $"scrcpy start: {exe} {args}");
        var process = await _processRunner.StartAsync(Key, exe, args, hidden: hidden, cancellationToken);
        if (process is null)
        {
            LogReceived?.Invoke(this, "ERR: scrcpy process could not be started.");
            return;
        }

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            _isPaused = false;
            LogReceived?.Invoke(this, $"scrcpy exited (code {process.ExitCode}).");
        };

        if (!hidden)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var iconSource = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "MirrorDeck.exe");
                    if (!File.Exists(iconSource))
                    {
                        iconSource = Path.Combine(AppContext.BaseDirectory, "MirrorDeck.exe");
                    }

                    await ProcessWindowBrandingHelper.TryBrandWindowAsync(process, "MirrorDeck - Android Mirror", iconSource, cancellationToken);
                }
                catch
                {
                    // Branding should never block screen mirroring startup.
                }
            }, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = false;
        return _processRunner.StopAsync(Key, force: true, timeout: TimeSpan.FromSeconds(6), cancellationToken);
    }

    public async Task RestartAsync(ScrcpyProfile profile, bool? hiddenOverride = null, CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(profile, hiddenOverride, cancellationToken);
    }

    public bool IsRunning() => _processRunner.IsRunning(Key);

    public bool IsPaused() => _isPaused;

    public Task<bool> TogglePauseAsync(CancellationToken cancellationToken = default)
    {
        if (!_processRunner.RunningProcesses.TryGetValue(Key, out var process))
        {
            _isPaused = false;
            return Task.FromResult(false);
        }

        var result = ProcessControlHelper.ToggleProcessPause(process, ref _isPaused);
        return Task.FromResult(result);
    }

    public Task<string?> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!_settingsService.Current.EnableAndroidService)
        {
            return Task.FromResult<string?>(null);
        }

        if (_processRunner.RunningProcesses.TryGetValue(Key, out var process))
        {
            var processCapture = ProcessControlHelper.CaptureProcessWindowByTitle(
                process.Id,
                _settingsService.Current.SnapshotDirectory,
                "MirrorDeck - Android Mirror",
                "scrcpy",
                "Android");

            processCapture ??= ProcessControlHelper.CaptureProcessWindow(process.Id, _settingsService.Current.SnapshotDirectory);
            return Task.FromResult<string?>(processCapture);
        }

        return Task.FromResult<string?>(null);
    }

    public string BuildArguments(ScrcpyProfile profile, AppSettings settings)
    {
        var args = new List<string>
        {
            $"--max-fps {Math.Clamp(settings.ScrcpyMaxFps, 10, 120)}",
            $"--max-size {Math.Clamp(settings.ScrcpyMaxSize, 640, 4320)}",
            $"--video-bit-rate {Math.Clamp(settings.ScrcpyBitrateKbps, 1000, 64000)}K"
        };

        if (settings.ScrcpyAlwaysOnTop)
        {
            args.Add("--always-on-top");
        }

        if (profile.MirrorOnly)
        {
            args.Add("--no-control");
        }

        if (!profile.EnableAudio)
        {
            args.Add("--no-audio");
        }

        if (profile.LowLatency)
        {
            args.Add("--display-buffer=20");
        }

        if (profile.PresentationMode)
        {
            args.Add("--window-title \"MirrorDeck Presentation\"");
        }
        else
        {
            args.Add("--window-title \"MirrorDeck - Android Mirror\"");
        }

        if (!string.IsNullOrWhiteSpace(profile.DeviceSerial))
        {
            args.Add($"--serial {profile.DeviceSerial}");
        }

        if (string.IsNullOrWhiteSpace(profile.DeviceSerial) && profile.SelectTcpIp)
        {
            args.Add("--select-tcpip");
        }
        else if (string.IsNullOrWhiteSpace(profile.DeviceSerial) && profile.SelectUsb)
        {
            args.Add("--select-usb");
        }

        return string.Join(" ", args);
    }

    public string GetExecutablePath()
    {
        var managed = FindExecutable(AppPaths.ScrcpyRoot, "scrcpy.exe");
        if (!string.IsNullOrWhiteSpace(managed))
        {
            return managed;
        }

        var installedRoot = Path.Combine(AppContext.BaseDirectory, "tools", "scrcpy");
        return FindExecutable(installedRoot, "scrcpy.exe") ?? Path.Combine(installedRoot, "scrcpy.exe");
    }

    private static string? FindExecutable(string root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return null;
        }

        var direct = Path.Combine(root, fileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        return Directory.GetFiles(root, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(path => File.Exists(path));
    }

    private void OnOutputReceived(object? sender, (string Source, string Line) e)
    {
        if (e.Source.Equals(Key, StringComparison.OrdinalIgnoreCase))
        {
            LogReceived?.Invoke(this, e.Line);
        }
    }

    private void OnErrorReceived(object? sender, (string Source, string Line) e)
    {
        if (e.Source.Equals(Key, StringComparison.OrdinalIgnoreCase))
        {
            LogReceived?.Invoke(this, $"ERR: {e.Line}");
        }
    }
}
