using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MirrorDeck.WinUI.Infrastructure;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace MirrorDeck.WinUI.Views;

public sealed class TrayMenuWindow : Window
{
    private const int MenuWidth = 214;
    private const int MenuMinHeight = 130;
    private const int MenuOuterPadding = 8;
    private const int ItemHeight = 34;
    private const int SeparatorHeight = 11;
    private const int ItemSpacing = 3;

    private readonly StackPanel _menuHost;
    private readonly Border _menuSurface;
    private readonly FrameworkElement _root;
    private Func<uint, Task>? _onItemInvoked;

    public TrayMenuWindow()
    {
        var shell = new Grid
        {
            Background = (Brush)Application.Current.Resources["MdSurfaceBrush"]
        };

        var root = new Border
        {
            Background = (Brush)Application.Current.Resources["MdSurfaceBrush"],
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(MenuOuterPadding)
        };

        _menuSurface = root;

        var layout = new StackPanel
        {
            Spacing = 6
        };

        var header = new Grid
        {
            Height = 28
        };

        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var logo = new Image
        {
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Source = new BitmapImage(new Uri("ms-appx:///Assets/MirrorDeckLogo.png"))
        };

        var title = new TextBlock
        {
            Text = "MirrorDeck",
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["MirrorDeckDisplayFont"],
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["MdTextStrongBrush"]
        };

        Grid.SetColumn(title, 1);
        header.Children.Add(logo);
        header.Children.Add(title);

        var headerDivider = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 0, 0, 2),
            Background = (Brush)Application.Current.Resources["MdBorderBrush"]
        };

        _menuHost = new StackPanel
        {
            Spacing = ItemSpacing,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        layout.Children.Add(header);
        layout.Children.Add(headerDivider);
        layout.Children.Add(_menuHost);

        root.Child = layout;
        shell.Children.Add(root);
        _root = shell;
        Content = shell;

        ConfigureWindowChrome();

        Activated += OnWindowActivated;
        Closed += OnWindowClosed;
        ThemeCoordinator.ThemeChanged += OnThemeChanged;
    }

    public void Initialize(IReadOnlyList<TrayMenuEntry> items, Func<uint, Task> onItemInvoked, PointInt32 anchor)
    {
        _onItemInvoked = onItemInvoked;

        var theme = ThemeCoordinator.CurrentElementTheme;
        ApplyTheme(theme);

        var menuHeight = RebuildMenu(items);

        try
        {
            AppWindow.Resize(new SizeInt32(MenuWidth, menuHeight));

            var displayArea = DisplayArea.GetFromPoint(anchor, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;

            var x = anchor.X - MenuWidth + 8;
            var y = anchor.Y - menuHeight - 10;

            // Clamp to the monitor work area so the last item is never clipped.
            x = Math.Max(workArea.X + 4, Math.Min(x, (workArea.X + workArea.Width) - MenuWidth - 4));
            y = Math.Max(workArea.Y + 4, Math.Min(y, (workArea.Y + workArea.Height) - menuHeight - 4));

            AppWindow.Move(new PointInt32(x, y));
        }
        catch
        {
            // Positioning is best-effort.
        }
    }

    private void ConfigureWindowChrome()
    {
        try
        {
            AppWindow.Title = string.Empty;

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            TryRemoveNativeWindowFrame();
        }
        catch
        {
            // Popup chrome setup is best-effort.
        }
    }

    private int RebuildMenu(IReadOnlyList<TrayMenuEntry> items)
    {
        _menuHost.Children.Clear();

        // Includes padding + branded header + divider + layout spacings to avoid clipping last item.
        var height = (MenuOuterPadding * 2) + 62;
        var first = true;

        foreach (var item in items)
        {
            if (!first)
            {
                height += ItemSpacing;
            }

            if (item.IsSeparator)
            {
                _menuHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(4, 5, 4, 5),
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["MdBorderBrush"]
                });

                height += SeparatorHeight;
                first = false;

                continue;
            }

            var button = new Button
            {
                Content = item.IsActive ? $"● {item.Label}" : item.Label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = item,
                Padding = new Thickness(12, 0, 12, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = ItemHeight,
                Height = ItemHeight,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["MirrorDeckBodyFont"],
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            ApplyButtonVisual(button, item);

            button.Click += OnMenuButtonClick;
            button.KeyDown += OnMenuButtonKeyDown;
            _menuHost.Children.Add(button);
            height += ItemHeight;
            first = false;
        }

        return Math.Max(MenuMinHeight, height);
    }

    private async void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not TrayMenuEntry entry || !entry.IsEnabled)
        {
            return;
        }

        try
        {
            if (_onItemInvoked is not null)
            {
                await _onItemInvoked(entry.Id);
            }
        }
        finally
        {
            Close();
        }
    }

    private async void OnMenuButtonKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        if (sender is not Button button || button.Tag is not TrayMenuEntry entry || !entry.IsEnabled)
        {
            return;
        }

        e.Handled = true;

        try
        {
            if (_onItemInvoked is not null)
            {
                await _onItemInvoked(entry.Id);
            }
        }
        finally
        {
            Close();
        }
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            try
            {
                Close();
            }
            catch
            {
                // Close is best-effort.
            }
        }
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
        _root.RequestedTheme = theme;
        _menuSurface.Background = (Brush)Application.Current.Resources["MdSurfaceBrush"];

        // Ensure already-visible disabled/enabled entries keep intended appearance after theme switch.
        foreach (var child in _menuHost.Children)
        {
            if (child is Button button && button.Tag is TrayMenuEntry entry)
            {
                ApplyButtonVisual(button, entry);
            }
        }
    }

    private static void ApplyButtonVisual(Button button, TrayMenuEntry entry)
    {
        var isEnabled = entry.IsEnabled;
        var isActive = entry.IsActive;
        var light = ThemeCoordinator.CurrentElementTheme == ElementTheme.Light;

        if (isEnabled)
        {
            if (entry.ActionKind == TrayActionKind.Start)
            {
                if (light)
                {
                    button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xD8 : (byte)0xE7, isActive ? (byte)0xF2 : (byte)0xF4, isActive ? (byte)0xDD : (byte)0xE9));
                    button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x49 : (byte)0x96, isActive ? (byte)0xA7 : (byte)0xC2, isActive ? (byte)0x6B : (byte)0xA8));
                    button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x0E : (byte)0x2B, isActive ? (byte)0x45 : (byte)0x6A, isActive ? (byte)0x22 : (byte)0x4A));
                    return;
                }

                button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x1F : (byte)0x22, isActive ? (byte)0x4A : (byte)0x3A, isActive ? (byte)0x2D : (byte)0x31));
                button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x61 : (byte)0x4E, isActive ? (byte)0xC0 : (byte)0x8A, isActive ? (byte)0x83 : (byte)0x72));
                button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xE9 : (byte)0xC6, isActive ? (byte)0xFF : (byte)0xE6, isActive ? (byte)0xF0 : (byte)0xD2));
                return;
            }

            if (entry.ActionKind == TrayActionKind.Stop)
            {
                if (light)
                {
                    button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xF8 : (byte)0xF3, isActive ? (byte)0xD6 : (byte)0xE4, isActive ? (byte)0xDA : (byte)0xE7));
                    button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xD6 : (byte)0xE2, isActive ? (byte)0x6A : (byte)0xBC, isActive ? (byte)0x78 : (byte)0xC3));
                    button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x5A : (byte)0x8C, isActive ? (byte)0x12 : (byte)0x56, isActive ? (byte)0x20 : (byte)0x60));
                    return;
                }

                button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x6A : (byte)0x3D, isActive ? (byte)0x2B : (byte)0x2A, isActive ? (byte)0x35 : (byte)0x30));
                button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xD0 : (byte)0x70, isActive ? (byte)0x70 : (byte)0x50, isActive ? (byte)0x82 : (byte)0x5A));
                button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xFF : (byte)0xC9, isActive ? (byte)0xEC : (byte)0xAE, isActive ? (byte)0xEE : (byte)0xB6));
                return;
            }

            if (entry.ActionKind == TrayActionKind.Restart)
            {
                if (light)
                {
                    button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xFF : (byte)0xF6, isActive ? (byte)0xE9 : (byte)0xEC, isActive ? (byte)0xC7 : (byte)0xDD));
                    button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xE4 : (byte)0xE5, isActive ? (byte)0xAA : (byte)0xD1, isActive ? (byte)0x55 : (byte)0xAB));
                    button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x5D : (byte)0x8A, isActive ? (byte)0x3B : (byte)0x6A, isActive ? (byte)0x0A : (byte)0x3A));
                    return;
                }

                button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0x5A : (byte)0x3B, isActive ? (byte)0x45 : (byte)0x34, isActive ? (byte)0x26 : (byte)0x2A));
                button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xE0 : (byte)0x6F, isActive ? (byte)0xA4 : (byte)0x5D, isActive ? (byte)0x4E : (byte)0x43));
                button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, isActive ? (byte)0xFF : (byte)0xC8, isActive ? (byte)0xF4 : (byte)0xBB, isActive ? (byte)0xDF : (byte)0xA6));
                return;
            }

            if (isActive)
            {
                if (light)
                {
                    button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x9B, 0xC3, 0xEA));
                    button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8E, 0xCC));
                    button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x0C, 0x22, 0x3A));
                    return;
                }

                button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2E, 0x57, 0x7E));
                button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6B, 0xB3, 0xF1));
                button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF4, 0xFA, 0xFF));
                return;
            }

            if (light)
            {
                button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xC5, 0xD4, 0xE6));
                button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x9F, 0xB4, 0xCB));
                button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x12, 0x2A, 0x43));
                return;
            }

            button.Background = (Brush)Application.Current.Resources["MdSecondaryButtonBrush"];
            button.BorderBrush = (Brush)Application.Current.Resources["MdBorderBrush"];
            button.Foreground = (Brush)Application.Current.Resources["MdTextStrongBrush"];
            return;
        }

        if (light)
        {
            button.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE2, 0xE9, 0xF2));
            button.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xCD, 0xD9, 0xE6));
            button.Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6A, 0x7C, 0x8F));
            return;
        }

        button.Background = (Brush)Application.Current.Resources["MdSurfaceAltBrush"];
        button.BorderBrush = (Brush)Application.Current.Resources["MdBorderBrush"];
        button.Foreground = (Brush)Application.Current.Resources["MdTextMutedBrush"];
    }

    private void TryRemoveNativeWindowFrame()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLongPtr(hwnd, GwlStyle).ToInt64();
        style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
        _ = SetWindowLongPtr(hwnd, GwlStyle, new IntPtr(style));

        var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        exStyle &= ~(WsExWindowEdge | WsExClientEdge | WsExStaticEdge | WsExDlgModalFrame);
        _ = SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle));

        _ = SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;

    private const long WsCaption = 0x00C00000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsThickFrame = 0x00040000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;

    private const long WsExDlgModalFrame = 0x00000001L;
    private const long WsExClientEdge = 0x00000200L;
    private const long WsExStaticEdge = 0x00020000L;
    private const long WsExWindowEdge = 0x00000100L;

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
