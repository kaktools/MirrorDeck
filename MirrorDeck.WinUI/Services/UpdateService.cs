using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.UpdateManagement;
using Windows.System;
using System.Diagnostics;

namespace MirrorDeck.WinUI.Services;

public sealed class UpdateService : IUpdateService
{
    private const string Owner = "kaktools";
    private const string Repository = "mirrordeck";
    private static readonly Uri ProjectUri = new("https://github.com/kaktools/mirrordeck");

    private readonly GitHubReleaseClient _releaseClient = new();
    private readonly ILoggingService _loggingService;
    private readonly IDownloadService _downloadService;

    public UpdateService(ILoggingService loggingService, IDownloadService downloadService)
    {
        _loggingService = loggingService;
        _downloadService = downloadService;
    }

    public async Task<MirrorDeckUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = VersionHelper.GetDisplayVersion();

        try
        {
            var latest = await _releaseClient.GetLatestReleaseAsync(Owner, Repository, cancellationToken);
            if (latest is null || string.IsNullOrWhiteSpace(latest.Version))
            {
                return new MirrorDeckUpdateInfo(currentVersion, null, false, null, ProjectUri);
            }

            var installerAsset = latest.Assets.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                (a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) || a.Name.Contains("installer", StringComparison.OrdinalIgnoreCase)));

            var downloadUri = installerAsset?.DownloadUrl;
            var releasePageUri = latest.ReleasePageUrl ?? ProjectUri;
            var isUpdateAvailable = VersionHelper.IsNewer(latest.Version, currentVersion);

            return new MirrorDeckUpdateInfo(currentVersion, latest.Version, isUpdateAvailable, downloadUri, releasePageUri);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("MirrorDeck update check failed", ex, "Update");
            return new MirrorDeckUpdateInfo(currentVersion, null, false, null, ProjectUri);
        }
    }

    public async Task<bool> InstallLatestUpdateAsync(CancellationToken cancellationToken = default)
    {
        var updateInfo = await CheckForUpdateAsync(cancellationToken);
        if (!updateInfo.IsUpdateAvailable)
        {
            return false;
        }

        if (updateInfo.InstallerDownloadUrl is not null)
        {
            try
            {
                var targetDirectory = Path.Combine(Path.GetTempPath(), "MirrorDeck", "updates");
                var installerPath = await _downloadService.DownloadFileAsync(updateInfo.InstallerDownloadUrl, targetDirectory, cancellationToken);

                _ = Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                });

                return true;
            }
            catch (Exception ex)
            {
                _loggingService.LogError("Direct installer download failed, opening release page instead", ex, "Update");
            }
        }

        var fallback = updateInfo.ReleasePageUrl ?? ProjectUri;
        return await Launcher.LaunchUriAsync(fallback);
    }

    public Task OpenProjectPageAsync()
    {
        return Launcher.LaunchUriAsync(ProjectUri).AsTask();
    }
}

public sealed record MirrorDeckUpdateInfo(
    string CurrentVersion,
    string? LatestVersion,
    bool IsUpdateAvailable,
    Uri? InstallerDownloadUrl,
    Uri? ReleasePageUrl);
