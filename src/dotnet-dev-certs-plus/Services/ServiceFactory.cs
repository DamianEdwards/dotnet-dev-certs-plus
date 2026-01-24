using DotnetDevCertsPlus.Services.Logging;
using Microsoft.Extensions.Logging;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Factory for creating service instances. Override in tests to provide mocks.
/// </summary>
public class ServiceFactory
{
    /// <summary>
    /// Default singleton instance.
    /// </summary>
    public static ServiceFactory Default { get; set; } = new();

    private ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Gets the logger factory, creating it if necessary.
    /// </summary>
    public virtual ILoggerFactory GetLoggerFactory()
    {
        if (_loggerFactory is not null)
        {
            return _loggerFactory;
        }

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            // File logging only for update checking (no console output)
            builder.AddFile(options =>
            {
                options.MinimumLevel = LogLevel.Debug;
                options.RetentionDays = 7;
            });
        });

        return _loggerFactory;
    }

    /// <summary>
    /// Creates a new process runner instance.
    /// </summary>
    public virtual IProcessRunner CreateProcessRunner() => ProcessRunner.Default;

    /// <summary>
    /// Creates a new dev cert service instance.
    /// </summary>
    public virtual IDevCertService CreateDevCertService() => new DevCertService(CreateProcessRunner());

    /// <summary>
    /// Creates a new machine store service instance.
    /// </summary>
    public virtual IMachineStoreService CreateMachineStoreService() => new MachineStoreService(CreateProcessRunner());

    /// <summary>
    /// Creates a new WSL service instance.
    /// </summary>
    public virtual IWslService CreateWslService() => new WslService(CreateProcessRunner());

    /// <summary>
    /// Creates a new update checker instance.
    /// </summary>
    public virtual IUpdateChecker CreateUpdateChecker() => new UpdateChecker(GetLoggerFactory());
}
