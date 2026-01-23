using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for querying GitHub Packages for package versions.
/// </summary>
public interface IGitHubPackagesClient
{
    /// <summary>
    /// Gets all available versions of the package from GitHub Packages.
    /// Returns empty list if authentication is not available.
    /// </summary>
    Task<IReadOnlyList<string>> GetPackageVersionsAsync(string owner, string packageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if GitHub authentication is available.
    /// </summary>
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for querying GitHub Packages API for NuGet package versions.
/// </summary>
public class GitHubPackagesClient : IGitHubPackagesClient
{
    private const string GitHubApiBaseUrl = "https://api.github.com";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly IProcessRunner _processRunner;
    private string? _cachedToken;

    /// <summary>
    /// Creates a new GitHubPackagesClient with default dependencies.
    /// </summary>
    public GitHubPackagesClient() : this(CreateDefaultHttpClient(), ProcessRunner.Default)
    {
    }

    /// <summary>
    /// Creates a new GitHubPackagesClient with custom dependencies (for testing).
    /// </summary>
    public GitHubPackagesClient(HttpClient httpClient, IProcessRunner processRunner)
    {
        _httpClient = httpClient;
        _processRunner = processRunner;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetPackageVersionsAsync(string owner, string packageName, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                return [];
            }

            var url = $"{GitHubApiBaseUrl}/users/{owner}/packages/nuget/{packageName}/versions";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var versions = await response.Content.ReadFromJsonAsync<List<PackageVersion>>(cancellationToken);
            
            return versions?
                .Where(v => !string.IsNullOrEmpty(v.Name))
                .Select(v => v.Name!)
                .ToList() ?? [];
        }
        catch
        {
            // Silently fail - update checking should not interrupt normal operation
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        return !string.IsNullOrEmpty(token);
    }

    private async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null)
        {
            return _cachedToken;
        }

        // First, check GITHUB_TOKEN environment variable
        var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
        {
            _cachedToken = envToken;
            return _cachedToken;
        }

        // Fall back to gh CLI
        try
        {
            var result = await _processRunner.RunAsync("gh", "auth token", cancellationToken);
            if (result.ExitCode == 0 && !string.IsNullOrEmpty(result.StandardOutput))
            {
                _cachedToken = result.StandardOutput.Trim();
                return _cachedToken;
            }
        }
        catch
        {
            // gh CLI not available or not authenticated
        }

        return null;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = DefaultTimeout
        };
        client.DefaultRequestHeaders.Add("User-Agent", "dotnet-dev-certs-plus");
        return client;
    }

    private sealed class PackageVersion
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
