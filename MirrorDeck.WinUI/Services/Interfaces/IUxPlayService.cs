using MirrorDeck.WinUI.Models;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IUxPlayService
{
    event EventHandler<string>? LogReceived;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RestartAsync(CancellationToken cancellationToken = default);
    Task<bool> TogglePauseAsync(CancellationToken cancellationToken = default);
    Task<string?> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
    bool IsRunning();
    bool IsPaused();
    string BuildArguments(AppSettings settings);
    string GetExecutablePath();
}
