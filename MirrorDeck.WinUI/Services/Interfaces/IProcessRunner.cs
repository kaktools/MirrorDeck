using System.Diagnostics;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IProcessRunner
{
    event EventHandler<(string Source, string Line)>? OutputReceived;
    event EventHandler<(string Source, string Line)>? ErrorReceived;

    Task<Process?> StartAsync(
        string key,
        string fileName,
        string arguments,
        bool hidden,
        CancellationToken cancellationToken = default);

    Task<bool> StopAsync(string key, bool force, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    bool IsRunning(string key);
    IReadOnlyDictionary<string, Process> RunningProcesses { get; }
}
