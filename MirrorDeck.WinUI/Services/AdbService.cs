using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MirrorDeck.WinUI.Infrastructure;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Services;

public sealed class AdbService : IAdbService
{
    public async Task<IReadOnlyList<string>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        var adb = GetAdbPath();
        if (!File.Exists(adb))
        {
            return [];
        }

        var output = await RunAdbAsync("devices", cancellationToken);
        return output
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Where(x => x.Contains("\tdevice", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Split('\t')[0].Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public async Task<bool> ConnectTcpAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var result = await ConnectTcpWithOutputAsync(host, port, cancellationToken);
        return result.Success;
    }

    public async Task<(bool Success, string Host, int Port, string Source, string Details)> DiscoverTcpEndpointAsync(int fallbackPort = 5555, CancellationToken cancellationToken = default)
    {
        try
        {
            var adb = GetAdbPath();
            if (!File.Exists(adb))
            {
                return (false, string.Empty, fallbackPort, "none", "adb.exe nicht gefunden.");
            }

            var devices = await GetConnectedDevicesAsync(cancellationToken);
            var usbDevices = devices.Where(d => !d.Contains(':')).ToList();

            // First choice: query WLAN IP from a USB-connected, authorized device.
            foreach (var serial in usbDevices)
            {
                var usbIp = await TryGetUsbDeviceWlanIpAsync(serial, cancellationToken);
                if (!string.IsNullOrWhiteSpace(usbIp))
                {
                    return (true, usbIp, fallbackPort, "usb", $"IP über USB-Gerät {serial} erkannt.");
                }
            }

            // Second choice: resolve endpoint from adb mDNS discovery.
            var mdnsOutput = await RunAdbAsync("mdns services", cancellationToken);
            if (TryParseAdbMdnsEndpoint(mdnsOutput, out var mdnsHost, out var mdnsPort))
            {
                return (true, mdnsHost, mdnsPort > 0 ? mdnsPort : fallbackPort, "mdns", "Endpoint über adb mDNS erkannt.");
            }

            // Last choice: if a TCP device is already connected, reuse its endpoint.
            foreach (var device in devices.Where(d => d.Contains(':')))
            {
                if (TryParseHostPort(device, out var host, out var port))
                {
                    return (true, host, port, "connected", "Bereits verbundenes TCP-Gerät übernommen.");
                }
            }

            return (false, string.Empty, fallbackPort, "none", "Kein Host automatisch gefunden. USB + Debugging prüfen.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, fallbackPort, "error", $"Auto-Erkennung fehlgeschlagen: {ex.Message}");
        }
    }

    public async Task<string> EnableTcpIpModeAsync(int port, CancellationToken cancellationToken = default)
    {
        var devices = await GetConnectedDevicesAsync(cancellationToken);
        var usbDevices = devices.Where(d => !d.Contains(':')).ToList();

        if (usbDevices.Count == 0)
        {
            return "No USB device found for adb tcpip step.";
        }

        var lines = new List<string>();
        foreach (var device in usbDevices)
        {
            var output = await RunAdbAsync($"-s {device} tcpip {port}", cancellationToken);
            if (!string.IsNullOrWhiteSpace(output))
            {
                lines.Add($"[{device}] {output}");
            }
        }

        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "adb tcpip command finished without output.";
    }

    public async Task<(bool Success, string Output)> ConnectTcpWithOutputAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var output = await RunAdbAsync($"connect {host}:{port}", cancellationToken);
        var success = output.Contains("connected", StringComparison.OrdinalIgnoreCase) ||
                      output.Contains("already connected", StringComparison.OrdinalIgnoreCase);
        return (success, output);
    }

    public async Task<bool> DisconnectTcpAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        var output = await RunAdbAsync($"disconnect {host}:{port}", cancellationToken);
        return output.Contains("disconnected", StringComparison.OrdinalIgnoreCase) || output.Contains("no such device", StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var adb = GetAdbPath();
        return Task.FromResult(File.Exists(adb));
    }

    public string GetAdbPath()
    {
        var managed = FindExecutable(AppPaths.ScrcpyRoot, "adb.exe");
        if (!string.IsNullOrWhiteSpace(managed))
        {
            return managed;
        }

        var installedRoot = Path.Combine(AppContext.BaseDirectory, "tools", "scrcpy");
        return FindExecutable(installedRoot, "adb.exe") ?? Path.Combine(installedRoot, "adb.exe");
    }

    private static string? FindExecutable(string root, string fileName)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return null;
        }

        var direct = Path.Combine(root, fileName);
        if (File.Exists(direct))
        {
            return direct;
        }

        return Directory.GetFiles(root, fileName, SearchOption.AllDirectories)
            .FirstOrDefault(path => File.Exists(path));
    }

    private async Task<string> RunAdbAsync(string args, CancellationToken cancellationToken)
    {
        var adb = GetAdbPath();
        if (!File.Exists(adb))
        {
            return string.Empty;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = adb,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return string.Empty;
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return string.Concat(stdout, Environment.NewLine, stderr).Trim();
    }

    private async Task<string?> TryGetUsbDeviceWlanIpAsync(string serial, CancellationToken cancellationToken)
    {
        var ipOutput = await RunAdbAsync($"-s {serial} shell ip -f inet addr show wlan0", cancellationToken);
        var parsedFromIp = ParseIpv4FromIpAddrOutput(ipOutput);
        if (!string.IsNullOrWhiteSpace(parsedFromIp))
        {
            return parsedFromIp;
        }

        var getPropOutput = await RunAdbAsync($"-s {serial} shell getprop dhcp.wlan0.ipaddress", cancellationToken);
        var parsedFromProp = ParseIpv4Token(getPropOutput);
        return string.IsNullOrWhiteSpace(parsedFromProp) ? null : parsedFromProp;
    }

    private static string? ParseIpv4FromIpAddrOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        foreach (var line in output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
        {
            var marker = "inet ";
            var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var candidate = line[(markerIndex + marker.Length)..].Trim();
            var slashIndex = candidate.IndexOf('/');
            if (slashIndex > 0)
            {
                candidate = candidate[..slashIndex];
            }

            var parsed = ParseIpv4Token(candidate);
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? ParseIpv4Token(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var normalized = token.Trim();
        if (IPAddress.TryParse(normalized, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return normalized;
        }

        return null;
    }

    private static bool TryParseAdbMdnsEndpoint(string output, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var lines = output
            .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        foreach (var line in lines)
        {
            if (!line.Contains("_adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("_adb._tcp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts.Reverse())
            {
                if (TryParseHostPort(part, out var parsedHost, out var parsedPort))
                {
                    host = parsedHost;
                    port = parsedPort;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseHostPort(string value, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().TrimEnd('.');
        var separatorIndex = trimmed.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
        {
            return false;
        }

        var hostPart = trimmed[..separatorIndex].Trim();
        var portPart = trimmed[(separatorIndex + 1)..].Trim();
        if (!int.TryParse(portPart, out var parsedPort) || parsedPort <= 0 || parsedPort > 65535)
        {
            return false;
        }

        // Allow IPv4 or hostnames (mDNS can return host names).
        if (string.IsNullOrWhiteSpace(hostPart))
        {
            return false;
        }

        host = hostPart;
        port = parsedPort;
        return true;
    }
}
