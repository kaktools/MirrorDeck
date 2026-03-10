using MirrorDeck.WinUI.Models;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IScrcpyService
{
    event EventHandler<string>? LogReceived;

    Task StartAsync(ScrcpyProfile profile, bool? hiddenOverride = null, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RestartAsync(ScrcpyProfile profile, bool? hiddenOverride = null, CancellationToken cancellationToken = default);
    Task<bool> TogglePauseAsync(CancellationToken cancellationToken = default);
    Task<string?> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
    bool IsRunning();
    bool IsPaused();
    string BuildArguments(ScrcpyProfile profile, AppSettings settings);
    string GetExecutablePath();
}
