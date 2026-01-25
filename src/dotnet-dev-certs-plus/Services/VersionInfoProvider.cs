using System.Reflection;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Default implementation that reads version from assembly metadata.
/// </summary>
public class VersionInfoProvider : IVersionInfoProvider
{
    private readonly Lazy<string> _version;

    public VersionInfoProvider()
    {
        _version = new Lazy<string>(() =>
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (version is not null)
            {
                var plusIndex = version.IndexOf('+');
                if (plusIndex > 0)
                {
                    version = version[..plusIndex];
                }
            }

            return version ?? "0.0.0";
        });
    }

    /// <inheritdoc/>
    public string GetCurrentVersion() => _version.Value;

    /// <inheritdoc/>
    public BuildType GetCurrentBuildType() => VersionInfo.GetBuildType(GetCurrentVersion());
}
