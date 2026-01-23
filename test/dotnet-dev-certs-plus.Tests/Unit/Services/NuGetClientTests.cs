using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotnetDevCertsPlus.Services;
using NSubstitute;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class NuGetClientTests
{
    [Fact]
    public async Task GetPackageVersionsAsync_WithValidResponse_ReturnsVersions()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        
        // Service index response - use raw JSON to properly include @ in property names
        handler.AddJsonResponse("https://api.nuget.org/v3/index.json", """
            {
                "resources": [
                    { "@id": "https://api.nuget.org/v3/registration/", "@type": "RegistrationsBaseUrl/3.6.0" }
                ]
            }
            """);

        // Registration response with inline items
        handler.AddJsonResponse("https://api.nuget.org/v3/registration/testpackage/index.json", """
            {
                "items": [
                    {
                        "items": [
                            { "catalogEntry": { "version": "1.0.0" } },
                            { "catalogEntry": { "version": "1.1.0" } },
                            { "catalogEntry": { "version": "2.0.0-pre.1" } }
                        ]
                    }
                ]
            }
            """);

        var client = new NuGetClient(handler.CreateClient());

        // Act
        var versions = await client.GetPackageVersionsAsync("testpackage", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, versions.Count);
        Assert.Contains("1.0.0", versions);
        Assert.Contains("1.1.0", versions);
        Assert.Contains("2.0.0-pre.1", versions);
    }

    [Fact]
    public async Task GetPackageVersionsAsync_WithPaginatedResponse_FetchesAllPages()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        
        handler.AddJsonResponse("https://api.nuget.org/v3/index.json", """
            {
                "resources": [
                    { "@id": "https://api.nuget.org/v3/registration/", "@type": "RegistrationsBaseUrl/3.6.0" }
                ]
            }
            """);

        // Registration with page reference (no inline items)
        handler.AddJsonResponse("https://api.nuget.org/v3/registration/testpackage/index.json", """
            {
                "items": [
                    { "@id": "https://api.nuget.org/v3/registration/testpackage/page1.json" }
                ]
            }
            """);

        // Page response
        handler.AddJsonResponse("https://api.nuget.org/v3/registration/testpackage/page1.json", """
            {
                "items": [
                    { "catalogEntry": { "version": "1.0.0" } },
                    { "catalogEntry": { "version": "1.1.0" } }
                ]
            }
            """);

        var client = new NuGetClient(handler.CreateClient());

        // Act
        var versions = await client.GetPackageVersionsAsync("testpackage", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, versions.Count);
        Assert.Contains("1.0.0", versions);
        Assert.Contains("1.1.0", versions);
    }

    [Fact]
    public async Task GetPackageVersionsAsync_HttpError_ReturnsEmptyList()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddErrorResponse("https://api.nuget.org/v3/index.json", HttpStatusCode.InternalServerError);

        var client = new NuGetClient(handler.CreateClient());

        // Act
        var versions = await client.GetPackageVersionsAsync("testpackage", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(versions);
    }

    [Fact]
    public async Task GetPackageVersionsAsync_PackageNotFound_ReturnsEmptyList()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        
        handler.AddResponse("https://api.nuget.org/v3/index.json", new
        {
            resources = new[]
            {
                new { @id = "https://api.nuget.org/v3/registration/", @type = "RegistrationsBaseUrl/3.6.0" }
            }
        });

        handler.AddErrorResponse("https://api.nuget.org/v3/registration/nonexistent/index.json", HttpStatusCode.NotFound);

        var client = new NuGetClient(handler.CreateClient());

        // Act
        var versions = await client.GetPackageVersionsAsync("nonexistent", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(versions);
    }

    /// <summary>
    /// Simple mock HTTP handler for testing.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _responses = new();

        public void AddResponse(string url, object content)
        {
            _responses[url] = () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(content), System.Text.Encoding.UTF8, "application/json")
            };
        }

        public void AddJsonResponse(string url, string jsonContent)
        {
            _responses[url] = () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        }

        public void AddErrorResponse(string url, HttpStatusCode statusCode)
        {
            _responses[url] = () => new HttpResponseMessage(statusCode);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            
            if (_responses.TryGetValue(url, out var responseFactory))
            {
                return Task.FromResult(responseFactory());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        public HttpClient CreateClient() => new(this)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
}
