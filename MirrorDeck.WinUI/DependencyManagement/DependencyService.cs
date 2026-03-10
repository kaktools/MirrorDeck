using MirrorDeck.WinUI.Helpers;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Models;
using MirrorDeck.WinUI.Services.Interfaces;
using MirrorDeck.WinUI.UpdateManagement;
using System.IO.Compression;

namespace MirrorDeck.WinUI.DependencyManagement;

public sealed class DependencyService : IDependencyService
{
    private const string UxPlayVersionMarker = "uxplay.version.txt";
    private const string ScrcpyVersionMarker = "scrcpy.version.txt";

    private readonly IUxPlayService _uxPlayService;
    private readonly IScrcpyService _scrcpyService;
    private readonly IAdbService _adbService;
    private readonly IBonjourService _bonjourService;
    private readonly IDownloadService _downloadService;
    private readonly ILoggingService _loggingService;
    private readonly GitHubReleaseClient _releaseClient = new();

    public DependencyService(
        IUxPlayService uxPlayService,
        IScrcpyService scrcpyService,
        IAdbService adbService,
        IBonjourService bonjourService,
        IDownloadService downloadService,
        ILoggingService loggingService)
    {
        _uxPlayService = uxPlayService;
        _scrcpyService = scrcpyService;
        _adbService = adbService;
        _bonjourService = bonjourService;
        _downloadService = downloadService;
        _loggingService = loggingService;
    }

    public async Task<DashboardStatus> GetDashboardStatusAsync(CancellationToken cancellationToken = default)
    {
        var uxInstalled = _uxPlayService.IsRunning() || File.Exists(_uxPlayService.GetExecutablePath());
        var scrcpyInstalled = File.Exists(_scrcpyService.GetExecutablePath());
        var adbAvailable = await _adbService.IsAvailableAsync(cancellationToken);
        var bonjourInstalled = await _bonjourService.IsInstalledAsync(cancellationToken);
        var bonjourRunning = await _bonjourService.IsRunningAsync(cancellationToken);

        var airPlayReady = uxInstalled && bonjourInstalled && bonjourRunning;
        var androidReady = scrcpyInstalled && adbAvailable;

        return new DashboardStatus
        {
            UxPlay = new ToolStatus
            {
                IsInstalled = uxInstalled,
                IsRunning = _uxPlayService.IsRunning(),
                Message = airPlayReady
                    ? "Bereit"
                    : !uxInstalled
                        ? "Fehlt: UxPlay"
                        : !bonjourInstalled
                            ? "Fehlt: Bonjour"
                            : "Bonjour gestoppt"
            },
            Scrcpy = new ToolStatus
            {
                IsInstalled = scrcpyInstalled,
                IsRunning = _scrcpyService.IsRunning(),
                Message = androidReady
                    ? "Bereit"
                    : !scrcpyInstalled
                        ? "Fehlt: scrcpy"
                        : "Fehlt: adb"
            },
            Adb = new ToolStatus
            {
                IsInstalled = adbAvailable,
                IsRunning = false,
                Message = adbAvailable ? "Verfugbar" : "Nicht verfugbar"
            },
            Bonjour = new ToolStatus
            {
                IsInstalled = bonjourInstalled,
                IsRunning = bonjourRunning,
                Message = !bonjourInstalled ? "Nicht installiert" : bonjourRunning ? "Installiert und aktiv" : "Installiert, aber gestoppt"
            }
        };
    }

    public async Task EnsureDependenciesAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var selection = new InstallSelection
        {
            InstallUxPlay = true,
            InstallScrcpy = true,
            InstallBonjourForAirPlay = true,
        };

        await InstallSelectedAsync(selection, progress, cancellationToken);
    }

    public async Task InstallSelectedAsync(InstallSelection selection, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();
        var archToken = Environment.Is64BitProcess ? "win64" : "win32";

        if (selection.IsEmpty)
        {
            progress?.Report("Keine Module ausgewahlt. Setup beendet.");
            return;
        }

        if (selection.InstallUxPlay)
        {
            progress?.Report("Checking UxPlay release...");
            var uxRelease = await _releaseClient.GetLatestReleaseAsync("FDH2", "UxPlay", cancellationToken);
            if (uxRelease is not null)
            {
                var zip = uxRelease.Assets.FirstOrDefault(a =>
                    a.Name.Contains("win", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                var currentVersion = ReadVersionMarker(Path.Combine(AppPaths.UxPlayRoot, UxPlayVersionMarker));
                var requiresUpdate = !File.Exists(_uxPlayService.GetExecutablePath()) || VersionHelper.IsNewer(uxRelease.Version, currentVersion);

                if (zip is not null && requiresUpdate)
                {
                    progress?.Report($"Downloading {zip.Name}...");
                    var downloaded = await _downloadService.DownloadFileAsync(zip.DownloadUrl, AppPaths.UxPlayRoot, cancellationToken);
                    ExtractZip(downloaded, AppPaths.UxPlayRoot);
                    WriteVersionMarker(Path.Combine(AppPaths.UxPlayRoot, UxPlayVersionMarker), uxRelease.Version);
                    TryDelete(downloaded);
                    progress?.Report("UxPlay downloaded and extracted.");
                    _loggingService.LogInfo($"UxPlay package downloaded: {downloaded}", "DependencyService");
                }
                else
                {
                    progress?.Report("UxPlay ist bereits aktuell oder kein passendes Asset gefunden.");
                }
            }
            else
            {
                progress?.Report("UxPlay Release-Informationen konnten nicht geladen werden.");
            }
        }

        if (selection.InstallScrcpy)
        {
            progress?.Report("Checking scrcpy release...");
            var scrcpyRelease = await _releaseClient.GetLatestReleaseAsync("Genymobile", "scrcpy", cancellationToken);
            if (scrcpyRelease is not null)
            {
                var zip = scrcpyRelease.Assets.FirstOrDefault(a =>
                    a.Name.Contains(archToken, StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                var currentVersion = ReadVersionMarker(Path.Combine(AppPaths.ScrcpyRoot, ScrcpyVersionMarker));
                var requiresUpdate = !File.Exists(_scrcpyService.GetExecutablePath()) || VersionHelper.IsNewer(scrcpyRelease.Version, currentVersion);

                if (zip is not null && requiresUpdate)
                {
                    progress?.Report($"Downloading {zip.Name}...");
                    var downloaded = await _downloadService.DownloadFileAsync(zip.DownloadUrl, AppPaths.ScrcpyRoot, cancellationToken);
                    ExtractZip(downloaded, AppPaths.ScrcpyRoot);
                    WriteVersionMarker(Path.Combine(AppPaths.ScrcpyRoot, ScrcpyVersionMarker), scrcpyRelease.Version);
                    TryDelete(downloaded);
                    progress?.Report("scrcpy downloaded and extracted.");
                    _loggingService.LogInfo($"scrcpy package downloaded: {downloaded}", "DependencyService");
                }
                else
                {
                    progress?.Report("scrcpy ist bereits aktuell oder kein passendes Asset gefunden.");
                }
            }
            else
            {
                progress?.Report("scrcpy Release-Informationen konnten nicht geladen werden.");
            }
        }

        if (selection.InstallBonjourForAirPlay)
        {
            progress?.Report("Prufe Bonjour Service...");
            var bonjourInstalled = await _bonjourService.IsInstalledAsync(cancellationToken);
            if (!bonjourInstalled)
            {
                progress?.Report("Lade Bonjour Installer...");
                var installerPath = await _bonjourService.DownloadInstallerAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(installerPath))
                {
                    progress?.Report("Bonjour Download fehlgeschlagen.");
                }
                else
                {
                    progress?.Report("Installiere Bonjour (Admin)...");
                    var installed = await _bonjourService.InstallDownloadedInstallerAsync(installerPath, cancellationToken);
                    if (!installed)
                    {
                        progress?.Report("Bonjour Installation fehlgeschlagen oder abgebrochen.");
                    }

                    bonjourInstalled = await _bonjourService.IsInstalledAsync(cancellationToken);
                }
            }

            if (bonjourInstalled && !await _bonjourService.IsRunningAsync(cancellationToken))
            {
                progress?.Report("Starte Bonjour Dienst...");
                await _bonjourService.StartAsync(cancellationToken);
            }
        }

        progress?.Report("Ausgewahlte Installation abgeschlossen.");
    }

    public async Task<string?> GetLatestUxPlayVersionAsync(CancellationToken cancellationToken = default)
    {
        var release = await _releaseClient.GetLatestReleaseAsync("FDH2", "UxPlay", cancellationToken);
        return release?.Version;
    }

    public async Task<string?> GetLatestScrcpyVersionAsync(CancellationToken cancellationToken = default)
    {
        var release = await _releaseClient.GetLatestReleaseAsync("Genymobile", "scrcpy", cancellationToken);
        return release?.Version;
    }

    private static string? ReadVersionMarker(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static void WriteVersionMarker(string path, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        File.WriteAllText(path, version.Trim());
    }

    private static void ExtractZip(string zipPath, string destination)
    {
        var tempExtract = Path.Combine(destination, "_extract_temp");
        if (Directory.Exists(tempExtract))
        {
            Directory.Delete(tempExtract, recursive: true);
        }

        Directory.CreateDirectory(tempExtract);
        ZipFile.ExtractToDirectory(zipPath, tempExtract, overwriteFiles: true);

        var root = Directory.GetDirectories(tempExtract).FirstOrDefault();
        var sourceRoot = root ?? tempExtract;

        foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        Directory.Delete(tempExtract, recursive: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Non-fatal cleanup path.
        }
    }
}
