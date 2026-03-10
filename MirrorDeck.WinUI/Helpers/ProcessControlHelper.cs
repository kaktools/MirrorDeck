using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace MirrorDeck.WinUI.Helpers;

internal static class ProcessControlHelper
{
    private const uint ProcessSuspendResume = 0x0800;
    private const int SwHide = 0;

    public static void HideProcessWindows(int pid)
    {
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid == pid && IsWindowVisible(hwnd))
            {
                ShowWindow(hwnd, SwHide);
            }

            return true;
        }, IntPtr.Zero);
    }

    public static bool ToggleProcessPause(System.Diagnostics.Process process, ref bool paused)
    {
        if (process.HasExited)
        {
            paused = false;
            return false;
        }

        var handle = OpenProcess(ProcessSuspendResume, false, (uint)process.Id);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var status = paused ? NtResumeProcess(handle) : NtSuspendProcess(handle);
            if (status == 0)
            {
                paused = !paused;
                return paused;
            }

            return false;
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    public static string CapturePrimaryScreen(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var outputPath = BuildUniqueSnapshotPath(targetDirectory, "MirrorDeck_Screen");

        var width = Math.Max(800, GetSystemMetrics(0));
        var height = Math.Max(600, GetSystemMetrics(1));
        var bounds = new Rectangle(0, 0, width, height);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        bitmap.Save(outputPath, ImageFormat.Png);

        return outputPath;
    }

    public static string CaptureProcessWindowOrDesktop(int pid, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var outputPath = BuildUniqueSnapshotPath(targetDirectory, $"MirrorDeck_Window_{pid}");

        if (!TryCaptureWindow(pid, outputPath))
        {
            throw new InvalidOperationException("No capturable process window found.");
        }

        return outputPath;
    }

    public static string? CaptureProcessWindow(int pid, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        var outputPath = BuildUniqueSnapshotPath(targetDirectory, $"MirrorDeck_Window_{pid}");
        return TryCaptureWindow(pid, outputPath) ? outputPath : null;
    }

    public static string? CaptureProcessWindowByTitle(int pid, string targetDirectory, params string[] preferredTitleFragments)
    {
        Directory.CreateDirectory(targetDirectory);
        var primaryTitle = preferredTitleFragments
            .FirstOrDefault(static t => !string.IsNullOrWhiteSpace(t))
            ?.Trim();

        var sanitizedTag = string.IsNullOrWhiteSpace(primaryTitle)
            ? $"Window_{pid}"
            : string.Concat(primaryTitle.Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_'));

        var outputPath = BuildUniqueSnapshotPath(targetDirectory, $"MirrorDeck_{sanitizedTag}");
        return TryCaptureWindow(pid, outputPath, preferredTitleFragments) ? outputPath : null;
    }

    private static string BuildUniqueSnapshotPath(string targetDirectory, string baseName)
    {
        var safeBaseName = string.IsNullOrWhiteSpace(baseName) ? "MirrorDeck" : baseName;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var candidate = Path.Combine(targetDirectory, $"{safeBaseName}_{timestamp}.png");

        var attempt = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(targetDirectory, $"{safeBaseName}_{timestamp}_{attempt}.png");
            attempt++;
        }

        return candidate;
    }

    public static int KillProcessesByName(string processName, bool entireProcessTree = true)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return 0;
        }

        var killed = 0;
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                process.Kill(entireProcessTree);
                killed++;
            }
            catch
            {
                // Best-effort shutdown fallback.
            }
        }

        return killed;
    }

    public static bool TryKillProcessByExecutablePath(string executablePath, bool entireProcessTree = true)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var normalizedTarget = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(normalizedTarget);

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                var currentPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(currentPath))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFullPath(currentPath), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.Kill(entireProcessTree);
                return true;
            }
            catch
            {
                // Best-effort shutdown fallback.
            }
        }

        return false;
    }

    private static bool TryCaptureWindow(int pid, string outputPath, params string[] preferredTitleFragments)
    {
        var candidates = new List<(IntPtr Hwnd, int Area, string Title)>();
        var preferredTargets = new List<(IntPtr Hwnd, int Area, string Title)>();
        var titleFilters = preferredTitleFragments
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .ToArray();

        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowPid);
            if (windowPid != pid || !IsWindowVisible(hwnd) || IsIconic(hwnd))
            {
                return true;
            }

            if (!TryGetWindowBounds(hwnd, out var bounds))
            {
                return true;
            }

            var width = Math.Max(0, bounds.Right - bounds.Left);
            var height = Math.Max(0, bounds.Bottom - bounds.Top);
            var area = width * height;
            if (area < 20_000)
            {
                return true;
            }

            var title = GetWindowTitle(hwnd);
            var candidate = (hwnd, area, title);
            candidates.Add(candidate);

            if (titleFilters.Length > 0 && titleFilters.Any(filter => title.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                preferredTargets.Add(candidate);
            }

            return true;
        }, IntPtr.Zero);

        var targetHwnd = preferredTargets
            .OrderByDescending(static x => x.Area)
            .Select(static x => x.Hwnd)
            .FirstOrDefault();

        if (targetHwnd == IntPtr.Zero)
        {
            targetHwnd = candidates
                .OrderByDescending(static x => x.Area)
                .Select(static x => x.Hwnd)
                .FirstOrDefault();
        }

        if (targetHwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!TryGetWindowBounds(targetHwnd, out var rect))
        {
            return false;
        }

        var width = Math.Max(10, rect.Right - rect.Left);
        var height = Math.Max(10, rect.Bottom - rect.Top);

        using var bitmap = new Bitmap(width, height);

        if (!TryRenderWindow(targetHwnd, bitmap))
        {
            return false;
        }

        bitmap.Save(outputPath, ImageFormat.Png);
        return true;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var sb = new StringBuilder(512);
        _ = GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static bool TryRenderWindow(IntPtr hwnd, Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        var hdc = graphics.GetHdc();

        try
        {
            const uint printWindowRenderFullContent = 0x00000002;

            if (PrintWindow(hwnd, hdc, printWindowRenderFullContent))
            {
                return true;
            }

            return PrintWindow(hwnd, hdc, 0);
        }
        finally
        {
            graphics.ReleaseHdc(hdc);
        }
    }

    private static bool TryGetWindowBounds(IntPtr hwnd, out RECT rect)
    {
        const int dwmwaExtendedFrameBounds = 9;
        var hr = DwmGetWindowAttribute(hwnd, dwmwaExtendedFrameBounds, out rect, Marshal.SizeOf<RECT>());
        if (hr == 0)
        {
            return true;
        }

        return GetWindowRect(hwnd, out rect);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);
}
