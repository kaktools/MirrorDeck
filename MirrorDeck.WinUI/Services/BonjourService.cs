using System.Diagnostics;
using System.ServiceProcess;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Services;

public sealed class BonjourService : IBonjourService
{
    private static readonly string[] CandidateServiceNames = ["Bonjour Service", "mDNSResponder"];
    private const string BonjourDownloadUrl = "https://download.info.apple.com/Mac_OS_X/061-8098.20100603.gthyu/BonjourPSSetup.exe";

    public Task<bool> IsInstalledAsync(CancellationToken cancellationToken = default)
    {
        var installed = TryResolveBonjourServiceName(out _);
        return Task.FromResult(installed);
    }

    public Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryResolveBonjourServiceName(out var serviceName))
            {
                return Task.FromResult(false);
            }

            using var service = new ServiceController(serviceName);
            return Task.FromResult(service.Status == ServiceControllerStatus.Running);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryResolveBonjourServiceName(out var serviceName))
            {
                return false;
            }

            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                return true;
            }

            try
            {
                service.Start();
                await Task.Run(() => service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10)), cancellationToken);
                return service.Status == ServiceControllerStatus.Running;
            }
            catch
            {
                // Fallback: attempt elevated service start.
                return await TryStartServiceElevatedAsync(serviceName, cancellationToken);
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RestartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryResolveBonjourServiceName(out var serviceName))
            {
                return false;
            }

            using var service = new ServiceController(serviceName);
            if (service.Status != ServiceControllerStatus.Stopped)
            {
                service.Stop();
                await Task.Run(() => service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10)), cancellationToken);
            }

            return await StartAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> DownloadInstallerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var downloadDir = Path.Combine(AppPaths.RootAppData, "downloads");
            Directory.CreateDirectory(downloadDir);
            var outputPath = Path.Combine(downloadDir, "BonjourPSSetup.exe");

            using var client = new HttpClient();
            using var response = await client.GetAsync(BonjourDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(outputPath);
            await input.CopyToAsync(output, cancellationToken);

            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> InstallDownloadedInstallerAsync(string installerPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(installerPath))
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/quiet /norestart",
                    UseShellExecute = true,
                    Verb = "runas",
                };

                var process = Process.Start(startInfo);
                if (process is not null)
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
            }

            if (await IsInstalledAsync(cancellationToken))
            {
                return true;
            }

            // Fallback: attempt installation via winget if available.
            var wingetOk = await TryInstallViaWingetAsync(cancellationToken);
            if (!wingetOk)
            {
                return false;
            }

            return await IsInstalledAsync(cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveBonjourServiceName(out string serviceName)
    {
        serviceName = string.Empty;

        try
        {
            var services = ServiceController.GetServices();

            foreach (var candidate in CandidateServiceNames)
            {
                var directMatch = services.FirstOrDefault(s =>
                    s.ServiceName.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (directMatch is not null)
                {
                    serviceName = directMatch.ServiceName;
                    return true;
                }
            }

            foreach (var candidate in CandidateServiceNames)
            {
                var displayMatch = services.FirstOrDefault(s =>
                    s.DisplayName.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (displayMatch is not null)
                {
                    serviceName = displayMatch.ServiceName;
                    return true;
                }
            }
        }
        catch
        {
            // Ignore probe failures and report not installed.
        }

        return false;
    }

    private static async Task<bool> TryStartServiceElevatedAsync(string serviceName, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c sc start \"{serviceName}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);

            using var service = new ServiceController(serviceName);
            service.Refresh();
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryInstallViaWingetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "install --id Apple.Bonjour --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

}
