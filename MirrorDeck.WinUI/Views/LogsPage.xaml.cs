using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MirrorDeck.WinUI.ViewModels;

namespace MirrorDeck.WinUI.Views;

public sealed partial class LogsPage : Page
{
    public LogsViewModel ViewModel { get; }

    public LogsPage()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<LogsViewModel>();
    }
}
