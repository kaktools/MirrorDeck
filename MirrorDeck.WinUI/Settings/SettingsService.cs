using System.Text.Json;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();

        if (!File.Exists(AppPaths.SettingsFile))
        {
            Current.ManagedToolsDirectory = AppPaths.ToolsRoot;
            Current.SnapshotDirectory = AppPaths.SnapshotRoot;
            Current.HasCompletedInitialModuleSetup = false;
            await SaveAsync(cancellationToken);
            return;
        }

        var json = await File.ReadAllTextAsync(AppPaths.SettingsFile, cancellationToken);
        Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        Current.ManagedToolsDirectory = string.IsNullOrWhiteSpace(Current.ManagedToolsDirectory) ? AppPaths.ToolsRoot : Current.ManagedToolsDirectory;
        Current.SnapshotDirectory = string.IsNullOrWhiteSpace(Current.SnapshotDirectory) ? AppPaths.SnapshotRoot : Current.SnapshotDirectory;
        Current.RunMirroringInBackgroundOnly = false;

    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();
        Current.RunMirroringInBackgroundOnly = false;
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        return File.WriteAllTextAsync(AppPaths.SettingsFile, json, cancellationToken);
    }

    public string GetSettingsPath() => AppPaths.SettingsFile;
}
