namespace MirrorDeck.WinUI.Models;

public sealed class LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
    public string Source { get; init; } = string.Empty;
    public string Level { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
}
