using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace MirrorDeck.WinUI.Views;

public sealed partial class SetupAssistantPage : Page
{
    private enum ShortcutTarget
    {
        None,
        Snapshot,
        PauseResume
    }

    public SetupAssistantViewModel ViewModel { get; }
    private readonly IUpdateService _updateService;
    private ShortcutTarget _captureTarget;
    private LicenseWindow? _licenseWindow;
    private bool _isUpdateAvailable;
    private bool _isCheckingForUpdate;

    public SetupAssistantPage()
    {
        InitializeComponent();
        ViewModel = App.Host.Services.GetRequiredService<SetupAssistantViewModel>();
        _updateService = App.Host.Services.GetRequiredService<IUpdateService>();
        Loaded += async (_, _) => await ViewModel.CheckBonjourAsync();
    }

    private void OnSnapshotShortcutFocus(object sender, RoutedEventArgs e)
    {
        _captureTarget = ShortcutTarget.Snapshot;
        ShortcutCaptureHint.Text = "Aufnahme aktiv: nächste Tastenkombination für Snapshot drücken.";
    }

    private void OnPauseShortcutFocus(object sender, RoutedEventArgs e)
    {
        _captureTarget = ShortcutTarget.PauseResume;
        ShortcutCaptureHint.Text = "Aufnahme aktiv: nächste Tastenkombination für Pause/Play drücken.";
    }

    private void OnClearSnapshotShortcut(object sender, RoutedEventArgs e)
    {
        ViewModel.SnapshotShortcut = string.Empty;
        _captureTarget = ShortcutTarget.None;
        ShortcutCaptureHint.Text = "Snapshot-Shortcut zurückgesetzt.";
    }

    private void OnClearPauseShortcut(object sender, RoutedEventArgs e)
    {
        ViewModel.PauseResumeShortcut = string.Empty;
        _captureTarget = ShortcutTarget.None;
        ShortcutCaptureHint.Text = "Pause/Play-Shortcut zurückgesetzt.";
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_captureTarget == ShortcutTarget.None)
        {
            return;
        }

        if (e.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu or VirtualKey.LeftWindows or VirtualKey.RightWindows)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            _captureTarget = ShortcutTarget.None;
            ShortcutCaptureHint.Text = "Aufnahme abgebrochen.";
            e.Handled = true;
            return;
        }

        var shortcut = BuildShortcutString(e.Key);
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return;
        }

        if (_captureTarget == ShortcutTarget.Snapshot)
        {
            ViewModel.SnapshotShortcut = shortcut;
            ShortcutCaptureHint.Text = $"Snapshot gesetzt: {shortcut}";
        }
        else
        {
            ViewModel.PauseResumeShortcut = shortcut;
            ShortcutCaptureHint.Text = $"Pause/Play gesetzt: {shortcut}";
        }

        _captureTarget = ShortcutTarget.None;
        Focus(FocusState.Programmatic);
        e.Handled = true;
    }

    private static string BuildShortcutString(VirtualKey key)
    {
        var parts = new List<string>();

        if (IsModifierDown(VirtualKey.Control))
        {
            parts.Add("Ctrl");
        }

        if (IsModifierDown(VirtualKey.Shift))
        {
            parts.Add("Shift");
        }

        if (IsModifierDown(VirtualKey.Menu))
        {
            parts.Add("Alt");
        }

        if (IsModifierDown(VirtualKey.LeftWindows) || IsModifierDown(VirtualKey.RightWindows))
        {
            parts.Add("Win");
        }

        var keyToken = NormalizeKeyToken(key);
        if (string.IsNullOrWhiteSpace(keyToken))
        {
            return string.Empty;
        }

        parts.Add(keyToken);
        return string.Join('+', parts);
    }

    private static bool IsModifierDown(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return state.HasFlag(CoreVirtualKeyStates.Down);
    }

    private static string NormalizeKeyToken(VirtualKey key)
    {
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            return key.ToString().ToUpperInvariant();
        }

        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            return ((int)key - (int)VirtualKey.Number0).ToString();
        }

        if (key >= VirtualKey.F1 && key <= VirtualKey.F24)
        {
            return key.ToString().ToUpperInvariant();
        }

        return key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Tab => "Tab",
            VirtualKey.Enter => "Enter",
            VirtualKey.Insert => "Insert",
            VirtualKey.Delete => "Delete",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Left => "Left",
            VirtualKey.Up => "Up",
            VirtualKey.Right => "Right",
            VirtualKey.Down => "Down",
            VirtualKey.Print => "PrintScreen",
            _ => string.Empty
        };
    }

    private void OnOpenLicenseWindowClicked(object sender, RoutedEventArgs e)
    {
        if (_licenseWindow is null)
        {
            _licenseWindow = new LicenseWindow();
            _licenseWindow.Closed += (_, _) => _licenseWindow = null;
        }

        _licenseWindow.Activate();
    }

    private async void OnCheckUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (_isCheckingForUpdate)
        {
            return;
        }

        _isCheckingForUpdate = true;
        var button = sender as Button;

        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            var update = await _updateService.CheckForUpdateAsync();
            _isUpdateAvailable = update.IsUpdateAvailable;
            UpdateInstallButton.IsEnabled = _isUpdateAvailable;

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
        finally
        {
            _isCheckingForUpdate = false;
            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void OnInstallUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (!_isUpdateAvailable)
        {
            UpdateInstallButton.IsEnabled = false;
            return;
        }

        var started = await _updateService.InstallLatestUpdateAsync();
        if (started)
        {
            return;
        }

        _isUpdateAvailable = false;
        UpdateInstallButton.IsEnabled = false;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Update",
            Content = "Es ist aktuell kein neuer Installer verfugbar.",
            CloseButtonText = "OK"
        };

        await dialog.ShowAsync();
    }

    private async void OnBuyMeCoffeeClicked(object sender, RoutedEventArgs e)
    {
        await Launcher.LaunchUriAsync(new Uri("https://buymeacoffee.com/kaktools"));
    }
}
