using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MirrorDeck.WinUI.ViewModels;

namespace MirrorDeck.WinUI.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<DashboardViewModel>();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }
}
