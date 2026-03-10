using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Services.Interfaces;
using Windows.System;

namespace MirrorDeck.WinUI.Views;

public sealed partial class FirstRunSetupPage : Page
{
    private readonly ISettingsService _settingsService;
    private bool _airPlaySelected;
    private bool _androidSelected;

    public FirstRunSetupPage()
    {
        InitializeComponent();
        _settingsService = App.Host.Services.GetRequiredService<ISettingsService>();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeCoordinator.ThemeChanged -= OnThemeChanged;
        ThemeCoordinator.ThemeChanged += OnThemeChanged;

        var settings = _settingsService.Current;
        _airPlaySelected = settings.EnableAirPlayService;
        _androidSelected = settings.EnableAndroidService;
        ApplyTheme(ThemeCoordinator.CurrentElementTheme);
        UpdateSelectionVisuals();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeCoordinator.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, ElementTheme theme)
    {
        var queue = App.UiDispatcherQueue;
        if (queue is not null && !queue.HasThreadAccess)
        {
            _ = queue.TryEnqueue(() => ApplyTheme(theme));
            return;
        }

        ApplyTheme(theme);
    }

    private void ApplyTheme(ElementTheme theme)
    {
        if (Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        UpdateSelectionVisuals();
    }

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        await CompleteSetupAsync(updateModuleSelection: true);
    }

    private void OnAirPlayCardClick(object sender, RoutedEventArgs e)
    {
        _airPlaySelected = !_airPlaySelected;
        UpdateSelectionVisuals();
    }

    private void OnAndroidCardClick(object sender, RoutedEventArgs e)
    {
        _androidSelected = !_androidSelected;
        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        var dark = ThemeCoordinator.CurrentElementTheme != ElementTheme.Light;

        ApplySelectionVisual(
            AirPlayCard,
            AirPlayIconHost,
            AirPlayStateHint,
            AirPlayIcon,
            _airPlaySelected,
            dark,
            isAirPlayCard: true);

        ApplySelectionVisual(
            AndroidCard,
            AndroidIconHost,
            AndroidStateHint,
            AndroidIcon,
            _androidSelected,
            dark,
            isAirPlayCard: false);
    }

    private async Task CompleteSetupAsync(bool updateModuleSelection)
    {
        var settings = _settingsService.Current;

        if (updateModuleSelection)
        {
            settings.EnableAirPlayService = _airPlaySelected;
            settings.EnableAndroidService = _androidSelected;
        }

        settings.HasCompletedInitialModuleSetup = true;

        await _settingsService.SaveAsync();

        if (Application.Current is App app && app.MainWindowInstance is MainWindow mainWindow)
        {
            mainWindow.CompleteFirstRunSelection();
        }
    }

    private async void OnBuyMeCoffeeClicked(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://buymeacoffee.com/kaktools"));
    }

    private static void ApplySelectionVisual(
        Button cardButton,
        Border iconHost,
        TextBlock stateHint,
        FontIcon icon,
        bool isSelected,
        bool dark,
        bool isAirPlayCard)
    {
        cardButton.BorderThickness = isSelected ? new Thickness(1.6) : new Thickness(1);

        if (isAirPlayCard)
        {
            cardButton.BorderBrush = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xA4, 0x66, 0xD8, 0xB7)
                        : Microsoft.UI.ColorHelper.FromArgb(0x3A, 0x4D, 0x67, 0x8A))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xC2, 0x37, 0xB0, 0x8C)
                        : Microsoft.UI.ColorHelper.FromArgb(0x36, 0x4A, 0x67, 0x89)));

            cardButton.Background = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xD2, 0x17, 0x36, 0x37)
                        : Microsoft.UI.ColorHelper.FromArgb(0xD4, 0x1A, 0x24, 0x38))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xEE, 0xE6, 0xF7, 0xF2)
                        : Microsoft.UI.ColorHelper.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)));

            iconHost.Background = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0x46, 0x1C, 0x73, 0x62)
                        : Microsoft.UI.ColorHelper.FromArgb(0x1E, 0x1C, 0x3E, 0x38))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0x5C, 0x68, 0xD1, 0xB2)
                        : Microsoft.UI.ColorHelper.FromArgb(0x35, 0x8E, 0xA7, 0xC2)));

            iconHost.BorderBrush = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0x9B, 0x74, 0xD8, 0xBC)
                        : Microsoft.UI.ColorHelper.FromArgb(0x36, 0x5F, 0xA3, 0x91))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xC2, 0x30, 0xA6, 0x85)
                        : Microsoft.UI.ColorHelper.FromArgb(0x5A, 0x3A, 0x90, 0xBC)));

            icon.Foreground = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE8, 0xFF, 0xF7)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xCE, 0xDE, 0xEE))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x18, 0x3C, 0x37)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x2E, 0x4C, 0x66)));

            stateHint.Foreground = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x8F, 0xE2, 0xC4)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x78, 0x90, 0xAC))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x1A, 0x8C, 0x67)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x5A, 0x71, 0x88)));
        }
        else
        {
            cardButton.BorderBrush = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xA8, 0xF4, 0xB5, 0x66)
                        : Microsoft.UI.ColorHelper.FromArgb(0x3A, 0x4D, 0x67, 0x8A))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xC8, 0xCE, 0x8A, 0x3A)
                        : Microsoft.UI.ColorHelper.FromArgb(0x36, 0x4A, 0x67, 0x89)));

            cardButton.Background = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xD4, 0x3D, 0x2B, 0x1F)
                        : Microsoft.UI.ColorHelper.FromArgb(0xD4, 0x1A, 0x24, 0x38))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xEE, 0xFA, 0xF0, 0xE5)
                        : Microsoft.UI.ColorHelper.FromArgb(0xF2, 0xFF, 0xFF, 0xFF)));

            iconHost.Background = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0x48, 0x7C, 0x54, 0x2E)
                        : Microsoft.UI.ColorHelper.FromArgb(0x1E, 0x3D, 0x34, 0x22))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0x5E, 0xEA, 0xBD, 0x84)
                        : Microsoft.UI.ColorHelper.FromArgb(0x32, 0xCF, 0xB5, 0x8C)));

            iconHost.BorderBrush = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xAB, 0xF1, 0xBC, 0x7A)
                        : Microsoft.UI.ColorHelper.FromArgb(0x36, 0x6E, 0x65, 0x50))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xCC, 0xB9, 0x74, 0x2A)
                        : Microsoft.UI.ColorHelper.FromArgb(0x5A, 0x8A, 0x77, 0x5E)));

            icon.Foreground = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xFF, 0xEE, 0xCC)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xD9, 0xD3, 0xBF))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x5A, 0x3A, 0x17)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x5E, 0x51, 0x38)));

            stateHint.Foreground = new SolidColorBrush(
                dark
                    ? (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF0, 0xBF, 0x84)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x78, 0x90, 0xAC))
                    : (isSelected
                        ? Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xA0, 0x5F, 0x16)
                        : Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x5A, 0x71, 0x88)));
        }

        stateHint.Text = isSelected ? "Aktiv" : "Inaktiv";
    }
}
