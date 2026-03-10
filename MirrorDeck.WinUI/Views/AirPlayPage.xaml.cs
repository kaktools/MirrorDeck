using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MirrorDeck.WinUI.ViewModels;

namespace MirrorDeck.WinUI.Views;

public sealed partial class AirPlayPage : Page
{
    public AirPlayViewModel ViewModel { get; }

    public AirPlayPage()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<AirPlayViewModel>();
    }
}
