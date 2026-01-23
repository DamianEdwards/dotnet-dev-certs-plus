namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for running external processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs an external process and returns the result.
    /// </summary>
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
