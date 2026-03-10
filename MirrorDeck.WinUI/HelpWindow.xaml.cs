using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Views;
using Windows.ApplicationModel;
using Windows.Graphics;

namespace MirrorDeck.WinUI;

public sealed partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        ThemeCoordinator.ThemeChanged += OnThemeChanged;
        Closed += OnWindowClosed;
        ApplyTheme(ThemeCoordinator.CurrentElementTheme);
        ContentFrame.Navigate(typeof(HelpPage));
    }

    private void ConfigureWindow()
    {
        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(PopupTitleBarDragRegion);
        }
        catch
        {
            // Best-effort titlebar customization.
        }

        try
        {
            AppWindow.Resize(new SizeInt32(LifecycleSplashWindow.SharedWindowWidth, LifecycleSplashWindow.SharedWindowHeight));
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = true;
                presenter.IsMinimizable = true;
                presenter.IsMaximizable = true;
            }
        }
        catch
        {
            // Best-effort window sizing.
        }

        TrySetWindowIcon(AppWindow);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
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
        try
        {
            RootLayout.RequestedTheme = theme;
        }
        catch
        {
            // Theme apply is best-effort.
        }

        ApplyTitleBarTheme(theme);
    }

    private void ApplyTitleBarTheme(ElementTheme theme)
    {
        try
        {
            var dark = theme != ElementTheme.Light;
            var titleBar = AppWindow.TitleBar;

            if (dark)
            {
                titleBar.BackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x12, 0x23, 0x3B);
                titleBar.ForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF4, 0xF8, 0xFF);
                titleBar.InactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x0E, 0x1D, 0x31);
                titleBar.InactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC8, 0xD3, 0xE6);
                titleBar.ButtonBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x12, 0x23, 0x3B);
                titleBar.ButtonForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF4, 0xF8, 0xFF);
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x1E, 0x39, 0x57);
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
                titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x0E, 0x1D, 0x31);
                titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC8, 0xD3, 0xE6);
                return;
            }

            titleBar.BackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF2, 0xF5, 0xFA);
            titleBar.ForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x1E, 0x2B, 0x3A);
            titleBar.InactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE7, 0xEC, 0xF3);
            titleBar.InactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x4A, 0x58, 0x68);
            titleBar.ButtonBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF2, 0xF5, 0xFA);
            titleBar.ButtonForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x1E, 0x2B, 0x3A);
            titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xD9, 0xE4, 0xF2);
            titleBar.ButtonHoverForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x0F, 0x1A, 0x28);
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xE7, 0xEC, 0xF3);
            titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x5A, 0x67, 0x76);
        }
        catch
        {
            // Title bar color setup is best-effort.
        }
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
