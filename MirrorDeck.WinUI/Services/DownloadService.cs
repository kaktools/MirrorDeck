using System.Net.Http.Headers;
using System.Security.Cryptography;
using MirrorDeck.WinUI.Services.Interfaces;

namespace MirrorDeck.WinUI.Services;

public sealed class DownloadService : IDownloadService
{
    private static readonly HttpClient HttpClient = new();

    static DownloadService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MirrorDeck", "0.1.0"));
    }

    public async Task<string> DownloadFileAsync(Uri url, string targetDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);

        var targetFile = Path.Combine(targetDirectory, Path.GetFileName(url.LocalPath));

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(targetFile);
        await input.CopyToAsync(output, cancellationToken);

        return targetFile;
    }

    public async Task<bool> ValidateSha256Async(string filePath, string expectedHash, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash);
        return actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
