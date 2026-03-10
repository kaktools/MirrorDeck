using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MirrorDeck.WinUI.Helpers;

internal static class ProcessWindowBrandingHelper
{
    private const int WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const uint GW_OWNER = 4;

    public static async Task<bool> WaitForMainWindowAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var checkEvery = TimeSpan.FromMilliseconds(250);
        var attempts = Math.Max(1, (int)(timeout.TotalMilliseconds / checkEvery.TotalMilliseconds));

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
            {
                return false;
            }

            if (FindMainWindow(process.Id) != IntPtr.Zero)
            {
                return true;
            }

            await Task.Delay(checkEvery, cancellationToken);
        }

        return false;
    }

    public static async Task<bool> TryBrandWindowAsync(Process process, string title, string iconSourceExecutable, CancellationToken cancellationToken = default)
    {
        if (process.HasExited)
        {
            return false;
        }

        var (largeIcon, smallIcon) = LoadIcons(iconSourceExecutable);
        var brandedAtLeastOnce = false;

        try
        {
            // Keep applying branding for a while because some sinks overwrite caption/icon after startup.
            for (var attempt = 0; attempt < 480; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (process.HasExited)
                {
                    return brandedAtLeastOnce;
                }

                var hwnd = FindMainWindow(process.Id);
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowText(hwnd, title);
                    ApplyWindowIcon(hwnd, largeIcon, smallIcon);
                    brandedAtLeastOnce = true;
                }

                await Task.Delay(250, cancellationToken);
            }
        }
        finally
        {
            if (smallIcon != IntPtr.Zero)
            {
                DestroyIcon(smallIcon);
            }

            if (largeIcon != IntPtr.Zero)
            {
                DestroyIcon(largeIcon);
            }
        }

        return brandedAtLeastOnce;
    }

    private static IntPtr FindMainWindow(int processId)
    {
        IntPtr preferred = IntPtr.Zero;
        IntPtr fallback = IntPtr.Zero;

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }

            if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero)
            {
                return true;
            }

            GetWindowThreadProcessId(hwnd, out var windowProcessId);
            if (windowProcessId != processId)
            {
                return true;
            }

            var titleLength = GetWindowTextLength(hwnd);
            if (titleLength <= 0)
            {
                if (fallback == IntPtr.Zero)
                {
                    fallback = hwnd;
                }

                return true;
            }

            var buffer = new StringBuilder(titleLength + 1);
            _ = GetWindowText(hwnd, buffer, buffer.Capacity);
            var title = buffer.ToString();

            if (title.Contains("Renderer", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("UxPlay", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("MirrorDeck", StringComparison.OrdinalIgnoreCase))
            {
                preferred = hwnd;
                return false;
            }

            if (fallback == IntPtr.Zero)
            {
                fallback = hwnd;
            }

            return true;
        }, IntPtr.Zero);

        return preferred != IntPtr.Zero ? preferred : fallback;
    }

    private static void ApplyWindowIcon(IntPtr hwnd, IntPtr largeIcon, IntPtr smallIcon)
    {
        if (smallIcon != IntPtr.Zero)
        {
            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, smallIcon);
        }

        if (largeIcon != IntPtr.Zero)
        {
            SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, largeIcon);
        }
    }

    private static (IntPtr Large, IntPtr Small) LoadIcons(string iconSourceExecutable)
    {
        if (string.IsNullOrWhiteSpace(iconSourceExecutable) || !File.Exists(iconSourceExecutable))
        {
            return (IntPtr.Zero, IntPtr.Zero);
        }

        var large = IntPtr.Zero;
        var small = IntPtr.Zero;
        _ = ExtractIconEx(iconSourceExecutable, 0, ref large, ref small, 1);
        return (large, small);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string szFileName, int nIconIndex, ref IntPtr phiconLarge, ref IntPtr phiconSmall, uint nIcons);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
