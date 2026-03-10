using Microsoft.UI.Xaml;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface ITrayService : IDisposable
{
    bool IsReady { get; }
    void Initialize(Window mainWindow);
    void ShowMainWindow();
    void HideMainWindow();
    void ShowInfoNotification(string title, string message);
    Task RefreshStatusAsync(CancellationToken cancellationToken = default);
}
