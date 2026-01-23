using System.Net;
using System.Text.Json;
using DotnetDevCertsPlus.Services;
using NSubstitute;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class GitHubPackagesClientTests
{
    [Fact]
    public async Task GetPackageVersionsAsync_WithGitHubToken_ReturnsVersions()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("https://api.github.com/users/testowner/packages/nuget/testpackage/versions", new[]
        {
            new { name = "1.0.0" },
            new { name = "1.1.0-pre.1.dev.5" },
            new { name = "2.0.0" }
        });

        var mockRunner = Substitute.For<IProcessRunner>();
        
        // Set up environment variable for token
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token");
        try
        {
            var client = new GitHubPackagesClient(handler.CreateClient(), mockRunner);

            // Act
            var versions = await client.GetPackageVersionsAsync("testowner", "testpackage", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(3, versions.Count);
            Assert.Contains("1.0.0", versions);
            Assert.Contains("1.1.0-pre.1.dev.5", versions);
            Assert.Contains("2.0.0", versions);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetPackageVersionsAsync_WithGhCli_ReturnsVersions()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddResponse("https://api.github.com/users/testowner/packages/nuget/testpackage/versions", new[]
        {
            new { name = "1.0.0" }
        });

        var mockRunner = Substitute.For<IProcessRunner>();
        mockRunner.RunAsync("gh", "auth token", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "gh-token-from-cli\n", ""));

        // Ensure no env var
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

        var client = new GitHubPackagesClient(handler.CreateClient(), mockRunner);

        // Act
        var versions = await client.GetPackageVersionsAsync("testowner", "testpackage", TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(versions);
        Assert.Contains("1.0.0", versions);
    }

    [Fact]
    public async Task GetPackageVersionsAsync_NoAuth_ReturnsEmptyList()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        var mockRunner = Substitute.For<IProcessRunner>();
        mockRunner.RunAsync("gh", "auth token", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "not logged in"));

        // Ensure no env var
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

        var client = new GitHubPackagesClient(handler.CreateClient(), mockRunner);

        // Act
        var versions = await client.GetPackageVersionsAsync("testowner", "testpackage", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(versions);
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithEnvVar_ReturnsTrue()
    {
        // Arrange
        var mockRunner = Substitute.For<IProcessRunner>();
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token");
        try
        {
            var client = new GitHubPackagesClient(new HttpClient(), mockRunner);

            // Act
            var result = await client.IsAuthenticatedAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithGhCli_ReturnsTrue()
    {
        // Arrange
        var mockRunner = Substitute.For<IProcessRunner>();
        mockRunner.RunAsync("gh", "auth token", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "token", ""));
        
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

        var client = new GitHubPackagesClient(new HttpClient(), mockRunner);

        // Act
        var result = await client.IsAuthenticatedAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAuthenticatedAsync_NoAuth_ReturnsFalse()
    {
        // Arrange
        var mockRunner = Substitute.For<IProcessRunner>();
        mockRunner.RunAsync("gh", "auth token", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", ""));
        
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

        var client = new GitHubPackagesClient(new HttpClient(), mockRunner);

        // Act
        var result = await client.IsAuthenticatedAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPackageVersionsAsync_HttpError_ReturnsEmptyList()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddErrorResponse("https://api.github.com/users/testowner/packages/nuget/testpackage/versions", HttpStatusCode.InternalServerError);

        var mockRunner = Substitute.For<IProcessRunner>();
        Environment.SetEnvironmentVariable("GITHUB_TOKEN", "test-token");
        try
        {
            var client = new GitHubPackagesClient(handler.CreateClient(), mockRunner);

            // Act
            var versions = await client.GetPackageVersionsAsync("testowner", "testpackage", TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(versions);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
        }
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
