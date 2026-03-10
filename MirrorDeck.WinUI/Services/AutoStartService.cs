using Microsoft.Win32;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Services;

public sealed class AutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "MirrorDeck";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        var value = key?.GetValue(EntryName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    public void Enable()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        key?.SetValue(EntryName, $"\"{exe}\"", RegistryValueKind.String);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (key?.GetValue(EntryName) is not null)
        {
            key.DeleteValue(EntryName, false);
        }
    }
}
