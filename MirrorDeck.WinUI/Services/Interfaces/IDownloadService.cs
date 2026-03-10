namespace MirrorDeck.WinUI.Services.Interfaces;

public interface IDownloadService
{
    Task<string> DownloadFileAsync(Uri url, string targetDirectory, CancellationToken cancellationToken = default);
    Task<bool> ValidateSha256Async(string filePath, string expectedHash, CancellationToken cancellationToken = default);
}
