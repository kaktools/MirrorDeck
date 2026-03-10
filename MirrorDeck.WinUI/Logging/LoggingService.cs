using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Logging;

public sealed class LoggingService : ILoggingService
{
    private const int RetentionDays = 3;
    private const long MaxLogFileBytes = 4L * 1024 * 1024;
    private const int KeepTailBytesOnTrim = 2 * 1024 * 1024;

    private readonly object _lock = new();
    private readonly List<LogEntry> _entries = [];

    public event EventHandler<LogEntry>? LogReceived;

    public void LogInfo(string message, string source = "MirrorDeck") => Write("Info", message, null, source);
    public void LogWarning(string message, string source = "MirrorDeck") => Write("Warning", message, null, source);
    public void LogError(string message, Exception? exception = null, string source = "MirrorDeck") => Write("Error", message, exception, source);

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    private void Write(string level, string message, Exception? exception, string source)
    {
        AppPaths.EnsureDirectories();

        var combined = exception is null ? message : $"{message}: {exception.Message}";
        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Message = combined,
            Source = source
        };

        lock (_lock)
        {
            CleanupLogFiles();

            _entries.Add(entry);
            if (_entries.Count > 1000)
            {
                _entries.RemoveAt(0);
            }

            TrimCurrentLogFileIfNeeded();

            var line = $"{entry.Timestamp:O} [{entry.Level}] [{entry.Source}] {entry.Message}{Environment.NewLine}";
            TryAppendLine(line);
        }

        LogReceived?.Invoke(this, entry);
    }

    private static void CleanupLogFiles()
    {
        var cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);

        foreach (var file in Directory.EnumerateFiles(AppPaths.LogDirectory, "mirrordeck-*.log", SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Retention cleanup must not break runtime logging.
            }
        }

        try
        {
            if (File.Exists(AppPaths.LegacyLogFile) && File.GetLastWriteTimeUtc(AppPaths.LegacyLogFile) < cutoffUtc)
            {
                File.Delete(AppPaths.LegacyLogFile);
            }
        }
        catch
        {
            // Legacy cleanup is best-effort.
        }
    }

    private static void TrimCurrentLogFileIfNeeded()
    {
        var path = AppPaths.LogFile;
        if (!File.Exists(path))
        {
            return;
        }

        var info = new FileInfo(path);
        if (info.Length <= MaxLogFileBytes)
        {
            return;
        }

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var tailSize = (int)Math.Min(KeepTailBytesOnTrim, fs.Length);
            fs.Seek(-tailSize, SeekOrigin.End);
            var buffer = new byte[tailSize];
            _ = fs.Read(buffer, 0, tailSize);

            var text = System.Text.Encoding.UTF8.GetString(buffer);
            var splitIndex = text.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            if (splitIndex >= 0)
            {
                text = text[(splitIndex + Environment.NewLine.Length)..];
            }

            File.WriteAllText(path, "[Info] Log file trimmed due to size limit." + Environment.NewLine + text);
        }
        catch
        {
            // Size trim is best-effort.
        }
    }

    private static void TryAppendLine(string line)
    {
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(line);
            using var fs = new FileStream(
                AppPaths.LogFile,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite | FileShare.Delete);
            fs.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
