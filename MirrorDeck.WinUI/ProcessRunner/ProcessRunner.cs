using System.Collections.Concurrent;
using System.Diagnostics;
using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.ProcessRunner;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly ConcurrentDictionary<string, Process> _processes = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<(string Source, string Line)>? OutputReceived;
    public event EventHandler<(string Source, string Line)>? ErrorReceived;

    public IReadOnlyDictionary<string, Process> RunningProcesses => _processes;

    public async Task<Process?> StartAsync(string key, string fileName, string arguments, bool hidden, CancellationToken cancellationToken = default)
    {
        if (_processes.TryGetValue(key, out var existing) && !existing.HasExited)
        {
            return existing;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // Keep the process detached from a console window even for visible mirror windows.
            CreateNoWindow = true,
            WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
        };

        var exeDir = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrWhiteSpace(exeDir) && Directory.Exists(exeDir))
        {
            startInfo.WorkingDirectory = exeDir;

            var extraPaths = new List<string>
            {
                exeDir,
                Path.Combine(exeDir, "bin"),
                Path.Combine(exeDir, "lib"),
                Path.Combine(exeDir, "lib", "gstreamer-1.0")
            }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (extraPaths.Count > 0)
            {
                startInfo.Environment["PATH"] = string.Join(";", extraPaths) + ";" + currentPath;
            }

            var gstPluginPath = Path.Combine(exeDir, "lib", "gstreamer-1.0");
            if (Directory.Exists(gstPluginPath))
            {
                startInfo.Environment["GST_PLUGIN_PATH"] = gstPluginPath;
                startInfo.Environment["GST_PLUGIN_SYSTEM_PATH_1_0"] = gstPluginPath;
            }
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                OutputReceived?.Invoke(this, (key, e.Data));
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                ErrorReceived?.Invoke(this, (key, e.Data));
            }
        };

        process.Exited += (_, _) => _processes.TryRemove(key, out _);

        var started = process.Start();
        if (!started)
        {
            return null;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _processes[key] = process;

        if (hidden)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(500, cancellationToken);
                ProcessControlHelper.HideProcessWindows(process.Id);
            }, cancellationToken);
        }

        await Task.Delay(150, cancellationToken);
        return process;
    }

    public async Task<bool> StopAsync(string key, bool force, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (!_processes.TryRemove(key, out var process))
        {
            return true;
        }

        try
        {
            if (process.HasExited)
            {
                return true;
            }

            if (!force)
            {
                process.CloseMainWindow();
            }

            var waitTask = Task.Run(() => process.WaitForExit((int)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds), cancellationToken);
            var exited = await waitTask;
            if (!exited && force)
            {
                process.Kill(true);
            }

            return process.HasExited;
        }
        catch
        {
            return false;
        }
        finally
        {
            process.Dispose();
        }
    }

    public bool IsRunning(string key)
    {
        return _processes.TryGetValue(key, out var process) && !process.HasExited;
    }
}
