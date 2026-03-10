using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.ViewModels;
using Windows.System;

namespace MirrorDeck.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }
    private readonly IUpdateService _updateService;
    private LicenseWindow? _licenseWindow;

    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<SettingsViewModel>();
        _updateService = App.Host.Services.GetRequiredService<IUpdateService>();
    }

    private void OnOpenLicenseWindowClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_licenseWindow is null)
        {
            _licenseWindow = new LicenseWindow();
            _licenseWindow.Closed += (_, _) => _licenseWindow = null;
        }

        _licenseWindow.Activate();
    }

    private async void OnCheckUpdateClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var update = await _updateService.CheckForUpdateAsync();
        var message = update.IsUpdateAvailable
            ? $"Neue Version verfugbar: {update.LatestVersion} (aktuell: {update.CurrentVersion})."
            : $"Kein Update gefunden. Aktuelle Version: {update.CurrentVersion}.";

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Update-Status",
            Content = message,
            CloseButtonText = "Schließen"
        };

        await dialog.ShowAsync();
    }

    private async void OnInstallUpdateClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var launched = await _updateService.InstallLatestUpdateAsync();
        if (launched)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Update",
            Content = "Es ist aktuell kein neuer Installer verfugbar.",
            CloseButtonText = "OK"
        };

        await dialog.ShowAsync();
    }

    private async void OnBuyMeCoffeeClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://buymeacoffee.com/kaktools"));
    }
}
