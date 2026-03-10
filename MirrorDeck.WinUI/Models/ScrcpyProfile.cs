namespace MirrorDeck.WinUI.Models;

public sealed class ScrcpyProfile
{
    public string Name { get; set; } = "Standard";
    public string? DeviceSerial { get; set; }
    public bool MirrorOnly { get; set; }
    public bool EnableAudio { get; set; }
    public bool LowLatency { get; set; }
    public bool PresentationMode { get; set; }
    public bool SelectUsb { get; set; }
    public bool SelectTcpIp { get; set; }
}
