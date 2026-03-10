using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel;
using Windows.Graphics;

namespace MirrorDeck.WinUI;

public sealed partial class FirstRunModuleWindow : Window
{
    private readonly TaskCompletionSource<(bool EnableAirPlay, bool EnableAndroid)> _resultTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FirstRunModuleWindow(bool defaultAirPlay, bool defaultAndroid)
    {
        InitializeComponent();
        ConfigureWindow();
        Closed += OnClosed;

        AirPlayToggle.IsChecked = defaultAirPlay;
        AndroidToggle.IsChecked = defaultAndroid;
    }

    public Task<(bool EnableAirPlay, bool EnableAndroid)> WaitForSelectionAsync()
    {
        return _resultTcs.Task;
    }

    private void OnUseDefaultsClick(object sender, RoutedEventArgs e)
    {
        AirPlayToggle.IsChecked = true;
        AndroidToggle.IsChecked = true;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var selection = (
            EnableAirPlay: AirPlayToggle.IsChecked != false,
            EnableAndroid: AndroidToggle.IsChecked != false);

        _resultTcs.TrySetResult(selection);
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        var fallback = (
            EnableAirPlay: AirPlayToggle.IsChecked != false,
            EnableAndroid: AndroidToggle.IsChecked != false);

        _resultTcs.TrySetResult(fallback);
    }

    private void ConfigureWindow()
    {
        // Keep default system title bar for startup stability.
        ExtendsContentIntoTitleBar = false;

        try
        {
            AppWindow.Resize(new SizeInt32(LifecycleSplashWindow.SharedWindowWidth, LifecycleSplashWindow.SharedWindowHeight));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }
        }
        catch
        {
            // Best-effort window sizing.
        }

        TrySetWindowIcon(AppWindow);
    }

    private static void TrySetWindowIcon(AppWindow appWindow)
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "MirrorDeck.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
                return;
            }

            var appxIconPath = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "MirrorDeck.ico");
            if (File.Exists(appxIconPath))
            {
                appWindow.SetIcon(appxIconPath);
            }
        }
        catch
        {
            // Icon assignment is best-effort.
        }
    }
}
