namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IAdbService
{
    Task<IReadOnlyList<string>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string Host, int Port, string Source, string Details)> DiscoverTcpEndpointAsync(int fallbackPort = 5555, CancellationToken cancellationToken = default);
    Task<string> EnableTcpIpModeAsync(int port, CancellationToken cancellationToken = default);
    Task<(bool Success, string Output)> ConnectTcpWithOutputAsync(string host, int port, CancellationToken cancellationToken = default);
    Task<bool> ConnectTcpAsync(string host, int port, CancellationToken cancellationToken = default);
    Task<bool> DisconnectTcpAsync(string host, int port, CancellationToken cancellationToken = default);
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    string GetAdbPath();
}
