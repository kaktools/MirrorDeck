using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MirrorDeck.WinUI.ViewModels;

namespace MirrorDeck.WinUI.Views;

public sealed partial class HelpPage : Page
{
    public HelpPage()
    {
        InitializeComponent();
        DataContext = App.Host.Services.GetRequiredService<HelpViewModel>();
    }
}
