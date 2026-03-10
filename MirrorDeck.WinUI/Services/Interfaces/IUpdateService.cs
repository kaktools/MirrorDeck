using MirrorDeck.WinUI.Services;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IUpdateService
{
    Task<MirrorDeckUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task<bool> InstallLatestUpdateAsync(CancellationToken cancellationToken = default);
    Task OpenProjectPageAsync();
}
