using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MirrorDeck.WinUI.Infrastructure;

public static class ThemeCoordinator
{
    private static string _currentTheme = "Dark";

    public static event EventHandler<ElementTheme>? ThemeChanged;

    public static string CurrentThemeSetting => _currentTheme;

    public static ElementTheme CurrentElementTheme => ParseTheme(_currentTheme);

    public static ElementTheme ParseTheme(string? theme)
    {
        if (string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase))
        {
            return ElementTheme.Light;
        }

        if (string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase))
        {
            return ElementTheme.Dark;
        }

        return ElementTheme.Default;
    }

    public static void ApplyTheme(string? theme)
    {
        _currentTheme = string.IsNullOrWhiteSpace(theme) ? "Dark" : theme.Trim();
        var elementTheme = ParseTheme(_currentTheme);

        var queue = App.UiDispatcherQueue;
        if (queue is not null && !queue.HasThreadAccess)
        {
            _ = queue.TryEnqueue(() => ApplyThemeOnUiThread(elementTheme));
            return;
        }

        ApplyThemeOnUiThread(elementTheme);
    }

    private static void ApplyThemeOnUiThread(ElementTheme theme)
    {
        if (Application.Current is not App app)
        {
            return;
        }

        ApplyPalette(theme);

        if (app.MainWindowInstance?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }

        ThemeChanged?.Invoke(null, theme);
    }

    private static void ApplyPalette(ElementTheme theme)
    {
        var dark = theme != ElementTheme.Light;
        var resources = Application.Current.Resources;

        SetBrush(resources, "MdBackgroundBrush", dark ? 0x0B1428u : 0xF4F7FBu);
        SetBrush(resources, "MdSurfaceBrush", dark ? 0x12233Bu : 0xFFFFFFu);
        SetBrush(resources, "MdSurfaceAltBrush", dark ? 0x0E1D31u : 0xEEF3F9u);
        SetBrush(resources, "MdBorderBrush", dark ? 0x30FFFFFFu : 0x260F2A45u);
        SetBrush(resources, "MdTextStrongBrush", dark ? 0xF4F8FFu : 0x14263Au);
        SetBrush(resources, "MdTextMutedBrush", dark ? 0xAFC4DAu : 0x48627Au);
        SetBrush(resources, "MdInfoBrush", dark ? 0x64C8FFu : 0x2D8FD6u);
        SetBrush(resources, "MdSuccessBrush", dark ? 0x2FBF78u : 0x1D9A62u);
        SetBrush(resources, "MdWarningBrush", dark ? 0xD79832u : 0xAD7A16u);
        SetBrush(resources, "MdDangerBrush", dark ? 0xE45A6Eu : 0xC1495Au);
        SetBrush(resources, "MdBadgeBorderBrush", dark ? 0x26FFFFFFu : 0x33283E54u);
        SetBrush(resources, "MdBadgeBackgroundBrush", dark ? 0x0DFFFFFFu : 0x14B8C7D9u);
        SetBrush(resources, "MdSecondaryButtonBrush", dark ? 0x2B3A52u : 0xBFD3E7u);
        SetBrush(resources, "MdNeutralButtonBrush", dark ? 0x334661u : 0xB4C9DFu);
        SetBrush(resources, "MdTitleBarBackgroundBrush", dark ? 0x12233Bu : 0xF2F5FAu);
        SetBrush(resources, "MdTitleBarSubtleTextBrush", dark ? 0xCFE0F5u : 0x4B5F73u);

        SetBrush(resources, "MirrorDeckPageBackgroundBrush", dark ? 0x0B1428u : 0xF4F7FBu);
        SetBrush(resources, "MirrorDeckBrandGradientBrush", dark ? 0x64C8FFu : 0x2D8FD6u);
        SetBrush(resources, "MirrorDeckTextStrongBrush", dark ? 0xF4F8FFu : 0x14263Au);
        SetBrush(resources, "MirrorDeckTextMutedBrush", dark ? 0xAFC4DAu : 0x48627Au);
        SetBrush(resources, "MirrorDeckCardBrush", dark ? 0x12233Bu : 0xFFFFFFu);
        SetBrush(resources, "MirrorDeckCardAltBrush", dark ? 0x0E1D31u : 0xEEF3F9u);
        SetBrush(resources, "MirrorDeckBorderBrush", dark ? 0x30FFFFFFu : 0x260F2A45u);
        SetBrush(resources, "MirrorDeckSuccessBrush", dark ? 0x2FBF78u : 0x1D9A62u);
        SetBrush(resources, "MirrorDeckWarningBrush", dark ? 0xD79832u : 0xAD7A16u);
        SetBrush(resources, "MirrorDeckDangerBrush", dark ? 0xE45A6Eu : 0xC1495Au);
    }

    private static void SetBrush(ResourceDictionary resources, string key, uint rgbOrArgb)
    {
        if (resources[key] is not SolidColorBrush brush)
        {
            return;
        }

        var color = rgbOrArgb > 0xFFFFFF
            ? Microsoft.UI.ColorHelper.FromArgb((byte)((rgbOrArgb >> 24) & 0xFF), (byte)((rgbOrArgb >> 16) & 0xFF), (byte)((rgbOrArgb >> 8) & 0xFF), (byte)(rgbOrArgb & 0xFF))
            : Microsoft.UI.ColorHelper.FromArgb(0xFF, (byte)((rgbOrArgb >> 16) & 0xFF), (byte)((rgbOrArgb >> 8) & 0xFF), (byte)(rgbOrArgb & 0xFF));

        brush.Color = color;
    }
}
