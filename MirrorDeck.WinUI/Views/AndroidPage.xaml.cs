using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MirrorDeck.WinUI.ViewModels;

namespace MirrorDeck.WinUI.Views;

public sealed partial class AndroidPage : Page
{
    public AndroidViewModel ViewModel { get; }

    public AndroidPage()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<AndroidViewModel>();
    }

    private void OnTcpPortValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        var rawValue = sender.Value;
        if (double.IsNaN(rawValue) || double.IsInfinity(rawValue))
        {
            sender.Value = ViewModel.TcpPort;
            return;
        }

        var normalized = (int)Math.Clamp(Math.Round(rawValue), 1, 65535);
        if (Math.Abs(sender.Value - normalized) > double.Epsilon)
        {
            sender.Value = normalized;
        }

        if (ViewModel.TcpPort != normalized)
        {
            ViewModel.TcpPort = normalized;
        }
    }
}
