using MirrorDeck.WinUI.Models;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IDependencyService
{
    Task<DashboardStatus> GetDashboardStatusAsync(CancellationToken cancellationToken = default);
    Task EnsureDependenciesAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task InstallSelectedAsync(InstallSelection selection, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    Task<string?> GetLatestUxPlayVersionAsync(CancellationToken cancellationToken = default);
    Task<string?> GetLatestScrcpyVersionAsync(CancellationToken cancellationToken = default);
}
