using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for querying NuGet.org for package versions.
/// </summary>
public interface INuGetClient
{
    /// <summary>
    /// Gets all available versions of the package from NuGet.org.
    /// </summary>
    Task<IReadOnlyList<string>> GetPackageVersionsAsync(string packageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Client for querying NuGet.org API for package versions.
/// </summary>
public class NuGetClient : INuGetClient
{
    private const string NuGetServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private const string RegistrationsBaseUrlType = "RegistrationsBaseUrl/3.6.0";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a new NuGetClient with a default HttpClient.
    /// </summary>
    public NuGetClient() : this(CreateDefaultHttpClient())
    {
    }

    /// <summary>
    /// Creates a new NuGetClient with a custom HttpClient (for testing).
    /// </summary>
    public NuGetClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetPackageVersionsAsync(string packageId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the service index to find the registrations base URL
            var registrationsBaseUrl = await GetRegistrationsBaseUrlAsync(cancellationToken);
            if (string.IsNullOrEmpty(registrationsBaseUrl))
            {
                return [];
            }

            // Query for package versions
            var registrationUrl = $"{registrationsBaseUrl.TrimEnd('/')}/{packageId.ToLowerInvariant()}/index.json";
            var registration = await _httpClient.GetFromJsonAsync<RegistrationIndex>(registrationUrl, cancellationToken);
            
            if (registration?.Items is null)
            {
                return [];
            }

            var versions = new List<string>();

            foreach (var page in registration.Items)
            {
                // If items are inline, use them directly
                if (page.Items is not null)
                {
                    versions.AddRange(page.Items
                        .Where(i => i.CatalogEntry?.Version is not null)
                        .Select(i => i.CatalogEntry!.Version!));
                }
                else if (!string.IsNullOrEmpty(page.Id))
                {
                    // Need to fetch the page
                    var pageData = await _httpClient.GetFromJsonAsync<RegistrationPage>(page.Id, cancellationToken);
                    if (pageData?.Items is not null)
                    {
                        versions.AddRange(pageData.Items
                            .Where(i => i.CatalogEntry?.Version is not null)
                            .Select(i => i.CatalogEntry!.Version!));
                    }
                }
            }

            return versions;
        }
        catch
        {
            // Silently fail - update checking should not interrupt normal operation
            return [];
        }
    }

    private async Task<string?> GetRegistrationsBaseUrlAsync(CancellationToken cancellationToken)
    {
        try
        {
            var serviceIndex = await _httpClient.GetFromJsonAsync<ServiceIndex>(NuGetServiceIndexUrl, cancellationToken);
            
            return serviceIndex?.Resources?
                .FirstOrDefault(r => r.Type == RegistrationsBaseUrlType)?
                .Id;
        }
        catch
        {
            return null;
        }
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

    // JSON models for NuGet API responses
    private sealed class ServiceIndex
    {
        [JsonPropertyName("resources")]
        public List<ServiceResource>? Resources { get; set; }
    }

    private sealed class ServiceResource
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("@type")]
        public string? Type { get; set; }
    }

    private sealed class RegistrationIndex
    {
        [JsonPropertyName("items")]
        public List<RegistrationPage>? Items { get; set; }
    }

    private sealed class RegistrationPage
    {
        [JsonPropertyName("@id")]
        public string? Id { get; set; }

        [JsonPropertyName("items")]
        public List<RegistrationLeaf>? Items { get; set; }
    }

    private sealed class RegistrationLeaf
    {
        [JsonPropertyName("catalogEntry")]
        public CatalogEntry? CatalogEntry { get; set; }
    }

    private sealed class CatalogEntry
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}
