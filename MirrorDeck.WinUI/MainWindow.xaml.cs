using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.ApplicationModel;
using Windows.UI;
using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.ViewModels;
using MirrorDeck.WinUI.Views;
using System.Runtime.InteropServices;

namespace MirrorDeck.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly ITrayService _trayService;
    private readonly ISettingsService _settingsService;
    private bool _allowClose;
    private bool _suppressSelectionChanged;
    private LogsWindow? _logsWindow;
    private HelpWindow? _helpWindow;
    private bool _initialNavigationApplied;
    private bool _isInFirstRunSelection;

    public MainWindow(ShellViewModel viewModel, ITrayService trayService, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _trayService = trayService;
        _settingsService = settingsService;

        var appWindow = AppWindow;
        appWindow.Title = "MirrorDeck - Unified Mirroring Control Center";
        TrySetWindowIcon(appWindow);
        ConfigureWindowSizing(appWindow);
        appWindow.Closing += OnAppWindowClosing;
        ConfigureWindowChrome();
        appWindow.Changed += OnAppWindowChanged;
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;

        ThemeCoordinator.ThemeChanged += OnThemeChanged;

        MainVersionText.Text = VersionHelper.GetDisplayVersion();
        OverlayVersionText.Text = VersionHelper.GetDisplayVersion();
    }

    private static void ConfigureWindowSizing(AppWindow appWindow)
    {
        try
        {
            appWindow.Resize(new SizeInt32(800, 580));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = true;
                presenter.IsMinimizable = true;
                presenter.IsMaximizable = true;
            }
        }
        catch
        {
            // Window sizing is best-effort.
        }
    }

    private void ConfigureWindowChrome()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBarDragRegion);
        CustomTitleBar.Visibility = Visibility.Visible;
        RootLayout.RowDefinitions[0].Height = GridLength.Auto;

        ApplyTitleBarTheme(CurrentRootTheme());

        UpdateTitleBarVersionBadgeMargin();
    }

    private void OnThemeChanged(object? sender, ElementTheme theme)
    {
        ApplyThemeToRoot(theme);
        ApplyTitleBarTheme(theme);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        ThemeCoordinator.ThemeChanged -= OnThemeChanged;
    }

    private ElementTheme CurrentRootTheme()
    {
        return RootLayout.RequestedTheme == ElementTheme.Default
            ? ThemeCoordinator.CurrentElementTheme
            : RootLayout.RequestedTheme;
    }

    private void ApplyThemeToRoot(ElementTheme theme)
    {
        try
        {
            RootLayout.RequestedTheme = theme;
            CustomTitleBar.RequestedTheme = theme;
        }
        catch
        {
            // Theme apply is best-effort.
        }
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

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange)
        {
            UpdateTitleBarVersionBadgeMargin();
        }
    }

    private void UpdateTitleBarVersionBadgeMargin()
    {
        try
        {
            var rightInset = Math.Max(92, AppWindow.TitleBar.RightInset + 8);
            MainVersionText.Margin = new Thickness(0, 0, rightInset, 0);
        }
        catch
        {
            // Margin alignment is best-effort only.
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        // Navigation is applied explicitly after settings were loaded,
        // so startup can happen in the same window without pre-navigation flicker.
    }

    public void ApplyInitialNavigation()
    {
        if (_initialNavigationApplied)
        {
            return;
        }

        _initialNavigationApplied = true;

        try
        {
            if (!_settingsService.Current.HasCompletedInitialModuleSetup)
            {
                _isInFirstRunSelection = true;
                AppNavigation.IsPaneVisible = false;
                ContentFrame.Navigate(typeof(FirstRunSetupPage));
                return;
            }

            if (AppNavigation.MenuItems.Count > 0)
            {
                AppNavigation.SelectedItem = AppNavigation.MenuItems[0];
            }
            else
            {
                ContentFrame.Navigate(typeof(DashboardPage));
            }
        }
        catch
        {
            // Initial navigation must not crash startup.
            try
            {
                ContentFrame.Navigate(typeof(DashboardPage));
            }
            catch
            {
                // Best-effort fallback only.
            }
        }
    }

    public async Task ShowStartupOverlayAsync()
    {
        ApplyStartupOverlayVisuals();
        LifecycleOverlay.Visibility = Visibility.Visible;
        LifecycleOverlay.IsHitTestVisible = true;
        await FadeElementAsync(LifecycleOverlay, from: LifecycleOverlay.Opacity, to: 1, durationMs: 220);
    }

    public async Task SetStartupProgressAsync(int progress, string stepText, int delayMs = 180)
    {
        OverlayStepText.Text = stepText;
        OverlayProgress.Value = Math.Clamp(progress, 0, 100);
        await Task.Delay(Math.Max(0, delayMs));
    }

    public async Task ShowMainContentAsync()
    {
        await Task.WhenAll(
            FadeElementAsync(RootLayout, from: RootLayout.Opacity, to: 1, durationMs: 260),
            FadeElementAsync(LifecycleOverlay, from: LifecycleOverlay.Opacity, to: 0, durationMs: 260));

        LifecycleOverlay.Visibility = Visibility.Collapsed;
        LifecycleOverlay.IsHitTestVisible = false;
    }

    public async Task ShowShutdownOverlayAsync()
    {
        ApplyShutdownOverlayVisuals();

        LifecycleOverlay.Visibility = Visibility.Visible;
        LifecycleOverlay.IsHitTestVisible = true;

        await Task.WhenAll(
            FadeElementAsync(LifecycleOverlay, from: LifecycleOverlay.Opacity, to: 1, durationMs: 220),
            FadeElementAsync(RootLayout, from: RootLayout.Opacity, to: 0, durationMs: 220));
    }

    public async Task SetShutdownProgressAsync(int progress, string stepText, double desaturationLevel, int delayMs = 220)
    {
        OverlayStepText.Text = stepText;
        OverlayProgress.Value = Math.Clamp(progress, 0, 100);
        ApplyShutdownDesaturation(desaturationLevel);
        await Task.Delay(Math.Max(0, delayMs));
    }

    public Task HideShutdownOverlayAsync(int durationMs = 320)
    {
        return FadeElementAsync(LifecycleOverlay, from: LifecycleOverlay.Opacity, to: 0, durationMs: durationMs);
    }

    private void ApplyStartupOverlayVisuals()
    {
        OverlayTitleText.Text = "MirrorDeck startet";
        OverlaySubtitleText.Text = "AirPlay und Android-Dienste werden vorbereitet";
        OverlayFooterText.Text = "Stabile Verbindungen für AirPlay, Android und Discovery";
        OverlayStepText.Text = "Initialisierung der Dienste...";

        OverlaySplashBackground.Opacity = 0.16;
        OverlayCenterBloom.Opacity = 0.52;
        OverlayShutdownTint.Opacity = 0;
        OverlayLogoGlow.Opacity = 0.42;

        OverlayTitleText.Foreground = BrushFromArgb(0xFF, 0xF0, 0xF6, 0xFF);
        OverlaySubtitleText.Foreground = BrushFromArgb(0xFF, 0xC1, 0xD2, 0xE9);
        OverlayStepText.Foreground = BrushFromArgb(0xFF, 0xD9, 0xE5, 0xF6);
        OverlayFooterText.Foreground = BrushFromArgb(0xFF, 0x8F, 0xA5, 0xC1);
        OverlayProgress.Foreground = BrushFromArgb(0xFF, 0x87, 0xCE, 0xFF);
        OverlayProgress.Background = BrushFromArgb(0x59, 0x2F, 0x43, 0x59);

        SetOverlayBadgeState(active: true);
        StartOverlayAmbientAnimations(startupMode: true);
    }

    private void ApplyShutdownOverlayVisuals()
    {
        OverlayTitleText.Text = "MirrorDeck wird beendet";
        OverlaySubtitleText.Text = "Dienste und Sessions werden sauber gestoppt";
        OverlayFooterText.Text = "Abschaltinitialisierung läuft kontrolliert";
        OverlayStepText.Text = "Beende aktive Verbindungen...";

        OverlayProgress.Value = 100;
        OverlayShutdownTint.Opacity = 0.36;
        SetOverlayBadgeState(active: false);
        StartOverlayAmbientAnimations(startupMode: false);
    }

    private void StartOverlayAmbientAnimations(bool startupMode)
    {
        _ = StartPulseAnimation(OverlayLogoGlow, from: startupMode ? 0.32 : 0.24, to: startupMode ? 0.5 : 0.34, durationMs: 1900);
        _ = StartPulseAnimation(OverlayCenterBloom, from: startupMode ? 0.44 : 0.36, to: startupMode ? 0.62 : 0.46, durationMs: 2500);
        _ = StartPulseAnimation(OverlaySplashBackground, from: startupMode ? 0.12 : 0.1, to: startupMode ? 0.2 : 0.14, durationMs: 3000);
    }

    private void SetOverlayBadgeState(bool active)
    {
        if (active)
        {
            SetOverlayBadge(OverlayAirPlayBadge, 0x26, 0x47, 0xB7, 0xA2, 0x6E, 0x92, 0xE3, 0xCF, 0xE9, 0xFF, 0xF8);
            SetOverlayBadge(OverlayAndroidBadge, 0x26, 0x48, 0x8F, 0xCF, 0x6E, 0x9C, 0xCD, 0xF6, 0xEA, 0xF5, 0xFF);
            SetOverlayBadge(OverlayDiscoveryBadge, 0x26, 0x3B, 0x67, 0xBD, 0x6E, 0x8E, 0xB2, 0xE5, 0xEA, 0xF2, 0xFF);
            return;
        }

        SetOverlayBadge(OverlayAirPlayBadge, 0x1D, 0x2C, 0x35, 0x47, 0x42, 0x4E, 0x61, 0x7A, 0xA8, 0xB5, 0xC8);
        SetOverlayBadge(OverlayAndroidBadge, 0x1D, 0x2A, 0x34, 0x46, 0x42, 0x4C, 0x60, 0x79, 0xA8, 0xB5, 0xC8);
        SetOverlayBadge(OverlayDiscoveryBadge, 0x1D, 0x28, 0x32, 0x45, 0x42, 0x4A, 0x5E, 0x78, 0xA8, 0xB5, 0xC8);
    }

    private void ApplyShutdownDesaturation(double level)
    {
        var t = Math.Clamp(level, 0, 1);

        OverlayTitleText.Foreground = new SolidColorBrush(BlendToGray(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xF0, 0xF6, 0xFF), t));
        OverlaySubtitleText.Foreground = new SolidColorBrush(BlendToGray(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC1, 0xD2, 0xE9), t));
        OverlayStepText.Foreground = new SolidColorBrush(BlendToGray(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xD9, 0xE5, 0xF6), t));
        OverlayFooterText.Foreground = new SolidColorBrush(BlendToGray(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x8F, 0xA5, 0xC1), t));

        OverlayProgress.Foreground = new SolidColorBrush(BlendToGray(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x87, 0xCE, 0xFF), t));
        OverlayProgress.Background = new SolidColorBrush(BlendToGray(Microsoft.UI.ColorHelper.FromArgb(0x59, 0x2F, 0x43, 0x59), t));

        OverlaySplashBackground.Opacity = 0.16 - (0.08 * t);
        OverlayCenterBloom.Opacity = 0.52 - (0.24 * t);
        OverlayLogoGlow.Opacity = 0.42 - (0.2 * t);
    }

    private static Color BlendToGray(Color color, double t)
    {
        var gray = (byte)Math.Clamp((0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B), 0, 255);
        var r = (byte)Math.Clamp(color.R + ((gray - color.R) * t), 0, 255);
        var g = (byte)Math.Clamp(color.G + ((gray - color.G) * t), 0, 255);
        var b = (byte)Math.Clamp(color.B + ((gray - color.B) * t), 0, 255);
        return Microsoft.UI.ColorHelper.FromArgb(color.A, r, g, b);
    }

    private static void SetOverlayBadge(
        Border badge,
        byte bgA,
        byte bgR,
        byte bgG,
        byte bgB,
        byte borderA,
        byte borderR,
        byte borderG,
        byte borderB,
        byte textR,
        byte textG,
        byte textB)
    {
        badge.Background = BrushFromArgb(bgA, bgR, bgG, bgB);
        badge.BorderBrush = BrushFromArgb(borderA, borderR, borderG, borderB);

        if (badge.Child is TextBlock text)
        {
            text.Foreground = BrushFromArgb(0xFF, textR, textG, textB);
        }
    }

    private static SolidColorBrush BrushFromArgb(byte a, byte r, byte g, byte b)
    {
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(a, r, g, b));
    }

    private static Task StartPulseAnimation(UIElement target, double from, double to, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(300, durationMs)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Begin();

        return Task.CompletedTask;
    }

    private static Task FadeElementAsync(UIElement element, double from, double to, int durationMs)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        element.Opacity = from;
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(80, durationMs))
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => tcs.TrySetResult();
        storyboard.Begin();
        return tcs.Task;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        if (_settingsService.Current.MinimizeToTrayOnClose)
        {
            if (!_trayService.IsReady)
            {
                args.Cancel = true;
                if (Application.Current is App missingTrayApp)
                {
                    _ = missingTrayApp.RequestControlledShutdownAsync("WindowCloseNoTray");
                }
                else
                {
                    _allowClose = true;
                    Close();
                }

                return;
            }

            args.Cancel = true;
            _trayService.HideMainWindow();
            return;
        }

        // Route normal close through controlled shutdown so the exit screen is shown.
        args.Cancel = true;
        if (Application.Current is App app)
        {
            _ = app.RequestControlledShutdownAsync("WindowCloseButton");
        }
        else
        {
            _allowClose = true;
            Close();
        }
    }

    public void ForceCloseFromTray()
    {
        _allowClose = true;
        Close();
    }

    public void NavigateTo(string destination)
    {
        switch (destination)
        {
            case "dashboard":
                ContentFrame.Navigate(typeof(DashboardPage));
                break;
            case "airplay":
                ContentFrame.Navigate(typeof(AirPlayPage));
                break;
            case "android":
                ContentFrame.Navigate(typeof(AndroidPage));
                break;
            case "setup":
                ContentFrame.Navigate(typeof(SetupAssistantPage));
                break;
            case "help":
                OpenHelpWindow();
                break;
            case "settings":
                ContentFrame.Navigate(typeof(SetupAssistantPage));
                break;
            case "logs":
                OpenLogsWindow();
                break;
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isInFirstRunSelection)
        {
            return;
        }

        if (_suppressSelectionChanged)
        {
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        if (tag is "logs" or "help")
        {
            NavigateTo(tag);
            RestoreNavigationSelection();
            return;
        }

        _viewModel.CurrentSection = tag;
        NavigateTo(tag);
    }

    public void CompleteFirstRunSelection()
    {
        _isInFirstRunSelection = false;
        AppNavigation.IsPaneVisible = true;
        _viewModel.CurrentSection = "dashboard";
        NavigateTo("dashboard");
        RestoreNavigationSelection();
    }

    private void RestoreNavigationSelection()
    {
        var targetTag = string.IsNullOrWhiteSpace(_viewModel.CurrentSection) ? "dashboard" : _viewModel.CurrentSection;
        var targetItem = AppNavigation.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, targetTag, StringComparison.OrdinalIgnoreCase));

        if (targetItem is null)
        {
            return;
        }

        _suppressSelectionChanged = true;
        AppNavigation.SelectedItem = targetItem;
        _suppressSelectionChanged = false;
    }

    private void OpenLogsWindow()
    {
        if (_logsWindow is not null)
        {
            _logsWindow.AppWindow.Show();
            _logsWindow.Activate();
            EnsureAuxiliaryWindowForeground(_logsWindow);
            return;
        }

        _logsWindow = new LogsWindow();
        _logsWindow.Closed += (_, _) => _logsWindow = null;
        _logsWindow.Activate();
        EnsureAuxiliaryWindowForeground(_logsWindow);
    }

    private void OpenHelpWindow()
    {
        if (_helpWindow is not null)
        {
            _helpWindow.AppWindow.Show();
            _helpWindow.Activate();
            EnsureAuxiliaryWindowForeground(_helpWindow);
            return;
        }

        _helpWindow = new HelpWindow();
        _helpWindow.Closed += (_, _) => _helpWindow = null;
        _helpWindow.Activate();
        EnsureAuxiliaryWindowForeground(_helpWindow);
    }

    private void EnsureAuxiliaryWindowForeground(Window window)
    {
        TryAssignMainWindowAsOwner(window);
        BringWindowToFront(window, this);
    }

    public void CloseAuxiliaryWindows()
    {
        try
        {
            _logsWindow?.Close();
        }
        catch
        {
            // Best-effort close.
        }

        try
        {
            _helpWindow?.Close();
        }
        catch
        {
            // Best-effort close.
        }

        _logsWindow = null;
        _helpWindow = null;
    }

    private static void BringWindowToFront(Window window, Window? ownerWindow)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (IsIconic(hwnd))
            {
                ShowWindow(hwnd, SwRestore);
            }
            else
            {
                ShowWindow(hwnd, SwShow);
            }

            if (ownerWindow is not null)
            {
                var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(ownerWindow);
                if (ownerHwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(ownerHwnd);
                }
            }

            SetWindowPos(hwnd, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
            SetForegroundWindow(hwnd);
        }
        catch
        {
            // Best-effort foreground behavior.
        }
    }

    private void TryAssignMainWindowAsOwner(Window childWindow)
    {
        try
        {
            var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var childHwnd = WinRT.Interop.WindowNative.GetWindowHandle(childWindow);

            if (ownerHwnd == IntPtr.Zero || childHwnd == IntPtr.Zero || ownerHwnd == childHwnd)
            {
                return;
            }

            SetWindowLongPtrCompat(childHwnd, GwlHwndParent, ownerHwnd);
        }
        catch
        {
            // Owner assignment is best-effort.
        }
    }

    private static IntPtr SetWindowLongPtrCompat(IntPtr hWnd, int index, IntPtr value)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, index, value);
        }

        return new IntPtr(SetWindowLong32(hWnd, index, value.ToInt32()));
    }

    private const int SwRestore = 9;
    private const int SwShow = 5;
    private const int GwlHwndParent = -8;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTop = IntPtr.Zero;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

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

            // Fallback for packaged lookup when running outside normal publish layout.
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
