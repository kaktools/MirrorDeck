using System.Net.Http.Headers;
using System.Text.Json;

namespace MirrorDeck.WinUI.UpdateManagement;

public sealed class GitHubReleaseClient
{
    private static readonly HttpClient HttpClient = new();

    static GitHubReleaseClient()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MirrorDeck", "0.1.0"));
    }

    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://api.github.com/repos/{owner}/{repository}/releases/latest";
        using var response = await HttpClient.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var pageUrl = root.TryGetProperty("html_url", out var pageElement) ? pageElement.GetString() : null;

        var assets = new List<GitHubReleaseAsset>();
        if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsElement.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var downloadUrl = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(downloadUrl))
                {
                    assets.Add(new GitHubReleaseAsset(name!, new Uri(downloadUrl!)));
                }
            }
        }

        var releasePageUrl = Uri.TryCreate(pageUrl, UriKind.Absolute, out var parsedPageUrl) ? parsedPageUrl : null;
        return new GitHubReleaseInfo(tag, assets, releasePageUrl);
    }
}

    public sealed record GitHubReleaseInfo(string? Version, IReadOnlyList<GitHubReleaseAsset> Assets, Uri? ReleasePageUrl = null);
public sealed record GitHubReleaseAsset(string Name, Uri DownloadUrl);
