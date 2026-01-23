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
}
