namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IBonjourService
{
    Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> IsRunningAsync(CancellationToken cancellationToken = default);
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
    Task<bool> RestartAsync(CancellationToken cancellationToken = default);
    Task<string?> DownloadInstallerAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallDownloadedInstallerAsync(string installerPath, CancellationToken cancellationToken = default);
}
