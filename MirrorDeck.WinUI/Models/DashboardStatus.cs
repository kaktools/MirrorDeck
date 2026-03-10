namespace MirrorDeck.WinUI.Models;

public sealed class DashboardStatus
{
    public ToolStatus UxPlay { get; set; } = new();
    public ToolStatus Bonjour { get; set; } = new();
    public ToolStatus Scrcpy { get; set; } = new();
    public ToolStatus Adb { get; set; } = new();

    public string LastConnection { get; set; } = "None";
    public string LastMode { get; set; } = "None";
}
