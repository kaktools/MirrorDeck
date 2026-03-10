using CommunityToolkit.Mvvm.ComponentModel;

namespace MirrorDeck.WinUI.ViewModels;

public class ShellViewModel : ObservableObject
{
    private string _currentSection = "dashboard";

    public string CurrentSection
    {
        get => _currentSection;
        set => SetProperty(ref _currentSection, value);
    }
}
