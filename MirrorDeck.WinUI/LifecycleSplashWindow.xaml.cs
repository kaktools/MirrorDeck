using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using MirrorDeck.WinUI.Helpers;
using Windows.Graphics;
using Windows.UI;

namespace MirrorDeck.WinUI;

public sealed partial class LifecycleSplashWindow : Window
{
    public const int SharedWindowWidth = 740;
    public const int SharedWindowHeight = 400;

    private readonly List<Storyboard> _ambientStoryboards = [];

    public LifecycleSplashWindow()
    {
        InitializeComponent();
        Closed += OnClosed;

        ConfigureSplashChrome();

        try
        {
            AppWindow.Resize(new SizeInt32(SharedWindowWidth, SharedWindowHeight));
            CenterOnScreen();
        }
        catch
        {
            // Best-effort size setup only.
        }

        SplashRoot.Opacity = 0;
        VersionText.Text = VersionHelper.GetDisplayVersion();
        StartAmbientAnimations();
    }

    private void CenterOnScreen()
    {
        try
        {
            var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var area = display.WorkArea;
            var x = area.X + Math.Max(0, (area.Width - SharedWindowWidth) / 2);
            var y = area.Y + Math.Max(0, (area.Height - SharedWindowHeight) / 2);
            AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch
        {
            // Keep default position if screen metrics are unavailable.
        }
    }

    private void ConfigureSplashChrome()
    {
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsResizable = false;
                presenter.SetBorderAndTitleBar(false, false);
            }
        }
        catch
        {
            // If chrome customization is unavailable on this platform, keep defaults.
        }
    }

    public Task PlayStartupAsync(CancellationToken cancellationToken = default)
    {
        ApplyStartupVisualMode();

        var steps = new List<(int Progress, string Text)>
        {
            (12, "Initialisierung der Dienste..."),
            (37, "AirPlay und Android werden vorbereitet..."),
            (63, "Discovery und Verbindungen werden aufgebaut..."),
            (86, "Letzte Initialisierungsschritte laufen..."),
            (100, "MirrorDeck ist bereit.")
        };
        SetBadgeStates(active: true);
        FooterText.Text = "Stabile Verbindungen für AirPlay, Android und Discovery";

        return PlaySequenceAsync(
            title: "MirrorDeck startet",
            subtitle: "AirPlay und Android-Dienste werden vorbereitet",
            steps: steps,
            titleFontSize: 44,
            delayMs: 340,
            cancellationToken: cancellationToken);
    }

    public async Task PlayShutdownAsync(CancellationToken cancellationToken = default)
    {
        await ApplyShutdownVisualModeAsync(cancellationToken);

        var steps = new List<(int Progress, string Text)>
        {
            (14, "Beende aktive Verbindungen..."),
            (41, "Dienste und Sessions werden gestoppt..."),
            (69, "Ressourcen werden freigegeben..."),
            (100, "MirrorDeck wurde beendet.")
        };
        SetBadgeStates(active: false);
        FooterText.Text = "Dienste und Sessions werden sauber gestoppt";

        await PlaySequenceAsync(
            title: "MirrorDeck wird beendet",
            subtitle: "Dienste und Sessions werden sauber gestoppt",
            steps: steps,
            titleFontSize: 40,
            delayMs: 320,
            cancellationToken: cancellationToken);
    }

    private async Task PlaySequenceAsync(
        string title,
        string subtitle,
        IReadOnlyList<(int Progress, string Text)> steps,
        double titleFontSize,
        int delayMs,
        CancellationToken cancellationToken)
    {
        TitleText.Text = title;
        TitleText.FontSize = titleFontSize;
        SubtitleText.Text = subtitle;
        LoadProgress.Value = 0;

        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StepText.Text = step.Text;
            LoadProgress.Value = Math.Clamp(step.Progress, 0, 100);
            await Task.Delay(delayMs, cancellationToken);
        }

        await Task.Delay(280, cancellationToken);
    }

    private void StartAmbientAnimations()
    {
        _ambientStoryboards.Add(StartAutoReverseAnimation(LogoGlow, "Opacity", 0.28, 0.5, 1800));
        _ambientStoryboards.Add(StartAutoReverseAnimation(CenterBloom, "Opacity", 0.44, 0.62, 2200));
        _ambientStoryboards.Add(StartAutoReverseAnimation(BackgroundSplash, "Opacity", 0.12, 0.2, 2800));
    }

    public void PrepareForClose()
    {
        StopAmbientAnimations();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        StopAmbientAnimations();
    }

    private void StopAmbientAnimations()
    {
        foreach (var storyboard in _ambientStoryboards)
        {
            try
            {
                storyboard.Stop();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        _ambientStoryboards.Clear();
    }

    private void ApplyStartupVisualMode()
    {
        ShutdownTintOverlay.Opacity = 0;
        BackgroundSplash.Opacity = 0.16;
        CenterBloom.Opacity = 0.52;
        LogoGlow.Opacity = 0.42;
        LogoGlow.Background = new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(0.5, 0.5),
            GradientOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RadiusX = 0.54,
            RadiusY = 0.54,
            GradientStops =
            {
                new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(0x6A, 0x77, 0xB9, 0xFF), Offset = 0 },
                new GradientStop { Color = Microsoft.UI.ColorHelper.FromArgb(0x08, 0x77, 0xB9, 0xFF), Offset = 1 }
            }
        };

        StepText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xD9, 0xE5, 0xF6));
        LoadProgress.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x87, 0xCE, 0xFF));
        LoadProgress.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x29, 0x2F, 0x43));
    }

    private async Task ApplyShutdownVisualModeAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            AnimateOpacityAsync(ShutdownTintOverlay, from: ShutdownTintOverlay.Opacity, to: 0.46, durationMs: 520, cancellationToken),
            AnimateOpacityAsync(CenterBloom, from: CenterBloom.Opacity, to: 0.4, durationMs: 520, cancellationToken),
            AnimateOpacityAsync(LogoGlow, from: LogoGlow.Opacity, to: 0.28, durationMs: 520, cancellationToken),
            AnimateOpacityAsync(BackgroundSplash, from: BackgroundSplash.Opacity, to: 0.12, durationMs: 520, cancellationToken));

        StepText.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xC9, 0xD7, 0xEA));
        LoadProgress.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x7D, 0xB9, 0xE6));
        LoadProgress.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x24, 0x2D, 0x3F));
    }

    private void SetBadgeStates(bool active)
    {
        if (active)
        {
            SetBadge(AirPlayBadge, 0x26, 0x47, 0xB7, 0xA2, 0x6E, 0x92, 0xE3, 0xCF, 0xE9, 0xFF, 0xF8);
            SetBadge(AndroidBadge, 0x26, 0x48, 0x8F, 0xCF, 0x6E, 0x9C, 0xCD, 0xF6, 0xEA, 0xF5, 0xFF);
            SetBadge(DiscoveryBadge, 0x26, 0x3B, 0x67, 0xBD, 0x6E, 0x8E, 0xB2, 0xE5, 0xEA, 0xF2, 0xFF);
            return;
        }

        SetBadge(AirPlayBadge, 0x1D, 0x2C, 0x35, 0x47, 0x42, 0x4E, 0x61, 0x7A, 0xA8, 0xB5, 0xC8);
        SetBadge(AndroidBadge, 0x1D, 0x2A, 0x34, 0x46, 0x42, 0x4C, 0x60, 0x79, 0xA8, 0xB5, 0xC8);
        SetBadge(DiscoveryBadge, 0x1D, 0x28, 0x32, 0x45, 0x42, 0x4A, 0x5E, 0x78, 0xA8, 0xB5, 0xC8);
    }

    private static void SetBadge(
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
        badge.Background = new SolidColorBrush(Color.FromArgb(bgA, bgR, bgG, bgB));
        badge.BorderBrush = new SolidColorBrush(Color.FromArgb(borderA, borderR, borderG, borderB));

        if (badge.Child is TextBlock text)
        {
            text.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, textR, textG, textB));
        }
    }

    public Task FadeInAsync(int durationMs = 240, CancellationToken cancellationToken = default)
    {
        return RunFadeAsync(0, 1, durationMs, cancellationToken);
    }

    public Task FadeOutAsync(int durationMs = 240, CancellationToken cancellationToken = default)
    {
        return RunFadeAsync(1, 0, durationMs, cancellationToken);
    }

    private Task RunFadeAsync(double from, double to, int durationMs, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        cancellationToken.ThrowIfCancellationRequested();

        SplashRoot.Opacity = from;
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(80, durationMs))
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, SplashRoot);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => tcs.TrySetResult();
        storyboard.Begin();

        return tcs.Task;
    }

    private static Task AnimateOpacityAsync(UIElement target, double from, double to, int durationMs, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        cancellationToken.ThrowIfCancellationRequested();

        target.Opacity = from;
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(120, durationMs))
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => tcs.TrySetResult();
        storyboard.Begin();

        return tcs.Task;
    }

    private static Storyboard StartAutoReverseAnimation(DependencyObject target, string property, double from, double to, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(240, durationMs)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
        storyboard.Begin();
        return storyboard;
    }
}
