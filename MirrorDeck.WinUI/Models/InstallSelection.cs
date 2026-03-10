namespace MirrorDeck.WinUI.Models;

public sealed class InstallSelection
{
    public bool InstallUxPlay { get; set; } = true;
    public bool InstallScrcpy { get; set; } = true;
    public bool InstallBonjourForAirPlay { get; set; } = true;

    public bool IsEmpty => !InstallUxPlay && !InstallScrcpy && !InstallBonjourForAirPlay;
}
