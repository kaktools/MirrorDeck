namespace MirrorDeck.WinUI.Models;

public sealed class ToolStatus
{
    public bool IsInstalled { get; set; }
    public bool IsRunning { get; set; }
    public string? Version { get; set; }
    public string Message { get; set; } = "Unknown";
}
