using MirrorDeck.WinUI.Models;

namespace MirrorDeck.WinUI.Services.Interfaces;

public interface ILoggingService
{
    event EventHandler<LogEntry>? LogReceived;

    void LogInfo(string message, string source = "MirrorDeck");
    void LogWarning(string message, string source = "MirrorDeck");
    void LogError(string message, Exception? exception = null, string source = "MirrorDeck");
    IReadOnlyList<LogEntry> Snapshot();
}
