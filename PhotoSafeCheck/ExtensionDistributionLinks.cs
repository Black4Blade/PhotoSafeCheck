using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoSafeCheck;

/// <summary>
/// Publisher-configurable install links. Invalid/missing URLs do not produce live buttons.
/// A release maintainer fills in ExtensionLinks.json after publication.
/// </summary>
public sealed class ExtensionDistributionLinks
{
    [JsonPropertyName("chromeWebStoreUrl")]
    public string? ChromeWebStoreUrl { get; set; }

    [JsonPropertyName("githubReleasesUrl")]
    public string? GitHubReleasesUrl { get; set; }

    [JsonIgnore]
    public bool HasChromeWebStore => IsStoreUrl(ChromeWebStoreUrl);

    [JsonIgnore]
    public bool HasGitHubReleases => IsGitHubReleaseUrl(GitHubReleasesUrl);

    public static ExtensionDistributionLinks Load()
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "ExtensionLinks.json");
            // Optional adjacent config overrides built-in defaults without rebuilding.
            if (File.Exists(file))
                return JsonSerializer.Deserialize<ExtensionDistributionLinks>(File.ReadAllText(file))
                       ?? new ExtensionDistributionLinks();
            using var packaged = BundledAssets.OpenLinks();
            return packaged is null ? new ExtensionDistributionLinks()
                : JsonSerializer.Deserialize<ExtensionDistributionLinks>(packaged)
                  ?? new ExtensionDistributionLinks();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log("Extension download link configuration unavailable: " + ex.Message);
            return new ExtensionDistributionLinks();
        }
    }

    public static bool IsStoreUrl(string? value)
    {
        if (!TryHttpsUrl(value, out var uri)) return false;
        var legacy = uri.Host.Equals("chrome.google.com", StringComparison.OrdinalIgnoreCase)
                     && uri.AbsolutePath.StartsWith("/webstore/detail/", StringComparison.OrdinalIgnoreCase);
        var current = uri.Host.Equals("chromewebstore.google.com", StringComparison.OrdinalIgnoreCase)
                      && uri.AbsolutePath.StartsWith("/detail/", StringComparison.OrdinalIgnoreCase);
        return legacy || current;
    }

    public static bool IsGitHubReleaseUrl(string? value)
    {
        if (!TryHttpsUrl(value, out var uri)) return false;
        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) return false;
        var p = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return p.Length >= 3 && p[2].Equals("releases", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryHttpsUrl(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var candidate)
            && candidate.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(candidate.UserInfo))
        {
            uri = candidate;
            return true;
        }
        uri = null!;
        return false;
    }
}
