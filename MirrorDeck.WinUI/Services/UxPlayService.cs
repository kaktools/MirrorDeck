using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Services;

public sealed class UxPlayService : IUxPlayService
{
    private const string Key = "uxplay";

    private readonly IProcessRunner _processRunner;
    private readonly ISettingsService _settingsService;
    private bool _isPaused;

    public event EventHandler<string>? LogReceived;

    public UxPlayService(IProcessRunner processRunner, ISettingsService settingsService)
    {
        _processRunner = processRunner;
        _settingsService = settingsService;

        _processRunner.OutputReceived += OnOutputReceived;
        _processRunner.ErrorReceived += OnErrorReceived;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        return StartInternalAsync(allowRecoveryRestart: true, cancellationToken);
    }

    private async Task StartInternalAsync(bool allowRecoveryRestart, CancellationToken cancellationToken)
    {
        var exe = GetExecutablePath();
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException("UxPlay executable not found", exe);
        }

        var args = BuildArguments(_settingsService.Current);
        var hidden = false;
        LogReceived?.Invoke(this, $"UxPlay start: {exe} {args}");
        var process = await _processRunner.StartAsync(Key, exe, args, hidden: hidden, cancellationToken);
        if (process is null)
        {
            LogReceived?.Invoke(this, "ERR: UxPlay process could not be started.");
            return;
        }

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            try
            {
                LogReceived?.Invoke(this, $"UxPlay exited (code {process.ExitCode}).");
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke(this, $"WARN: UxPlay exit callback failed: {ex.Message}");
            }
        };

        if (!hidden)
        {
            var hasWindow = await ProcessWindowBrandingHelper.WaitForMainWindowAsync(process, TimeSpan.FromSeconds(7), cancellationToken);
            if (!hasWindow && allowRecoveryRestart)
            {
                LogReceived?.Invoke(this, "WARN: UxPlay window not detected after startup. Retrying once...");
                await StopAsync(cancellationToken);
                await Task.Delay(500, cancellationToken);
                await StartInternalAsync(allowRecoveryRestart: false, cancellationToken);
                return;
            }
        }

        if (!hidden && process is not null)
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

                    await ProcessWindowBrandingHelper.TryBrandWindowAsync(process, "MirrorDeck - Airplay Mirror", iconSource, cancellationToken);
                }
                catch
                {
                    // Receiver branding is best-effort and must never block startup.
                }
            }, cancellationToken);
        }

        _isPaused = false;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isPaused = false;
        return _processRunner.StopAsync(Key, force: true, timeout: TimeSpan.FromSeconds(6), cancellationToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartInternalAsync(allowRecoveryRestart: true, cancellationToken);
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
        if (!_settingsService.Current.EnableAirPlayService)
        {
            return Task.FromResult<string?>(null);
        }

        if (_processRunner.RunningProcesses.TryGetValue(Key, out var process))
        {
            var processCapture = ProcessControlHelper.CaptureProcessWindowByTitle(
                process.Id,
                _settingsService.Current.SnapshotDirectory,
                "MirrorDeck - Airplay Mirror",
                "UxPlay",
                "AirPlay");

            processCapture ??= ProcessControlHelper.CaptureProcessWindow(process.Id, _settingsService.Current.SnapshotDirectory);
            return Task.FromResult<string?>(processCapture);
        }

        return Task.FromResult<string?>(null);
    }

    public string BuildArguments(AppSettings settings)
    {
        var arguments = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.AirPlayName))
        {
            arguments.Add($"-n \"{settings.AirPlayName}\"");
        }

        if (settings.AirPlayFullscreen)
        {
            arguments.Add("-fs");
        }

        if (!settings.AirPlayAudioEnabled)
        {
            arguments.Add("-a");
        }

        if (!string.IsNullOrWhiteSpace(settings.UxPlayExtraArgs))
        {
            arguments.Add(settings.UxPlayExtraArgs.Trim());
        }

        return string.Join(" ", arguments);
    }

    public string GetExecutablePath()
    {
        var managed = FindExecutable(AppPaths.UxPlayRoot, "uxplay.exe");
        if (!string.IsNullOrWhiteSpace(managed))
        {
            return managed;
        }

        var installedRoot = Path.Combine(AppContext.BaseDirectory, "tools", "uxplay");
        return FindExecutable(installedRoot, "uxplay.exe") ?? Path.Combine(installedRoot, "uxplay.exe");
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
