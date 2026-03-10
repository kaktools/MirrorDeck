using MirrorDeck.WinUI.Models;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    string GetSettingsPath();
}
