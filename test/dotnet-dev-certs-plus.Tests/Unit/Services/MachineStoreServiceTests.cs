using System.Reflection;
using DotnetDevCertsPlus.Services;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class MachineStoreServiceTests
{
    [Fact]
    public void CertFriendlyName_MatchesDotnetDevCertsValue()
    {
        // The friendly name should match what dotnet dev-certs https uses
        var expectedFriendlyName = "ASP.NET Core HTTPS development certificate";

        // Use reflection to verify the constant value
        var field = typeof(MachineStoreService)
            .GetField("CertFriendlyName", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var actualValue = field.GetValue(null) as string;
        Assert.Equal(expectedFriendlyName, actualValue);
    }

    [Fact]
    public void DefaultConstructor_CreatesInstance()
    {
        // Act
        var service = new MachineStoreService();

        // Assert
        Assert.NotNull(service);
    }
}
