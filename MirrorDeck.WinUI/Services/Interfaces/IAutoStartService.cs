namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IAutoStartService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
