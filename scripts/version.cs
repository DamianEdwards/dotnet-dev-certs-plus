#!/usr/bin/env dotnet run
#:package NuGet.Versioning@6.13.1
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NuGet.Versioning;

// JSON serialization options
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

// Parse command and arguments
if (args.Length == 0)
{
    PrintHelp();
    return 0;
}

var command = args[0].ToLower();
var cmdArgs = ParseArgs(args.Skip(1).ToArray());

try
{
    return command switch
    {
        "state" => HandleState(cmdArgs),
        "calculate" => HandleCalculate(cmdArgs),
        "advance" => HandleAdvance(cmdArgs),
        "bump" => HandleBump(cmdArgs),
        "validate" => HandleValidate(cmdArgs),
        "--help" or "-h" or "help" => PrintHelp(),
        _ => Error($"Unknown command: {command}")
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

// === COMMAND HANDLERS ===

int HandleState(Dictionary<string, string?> args)
{
    VersionState state;

    if (args.TryGetValue("--body", out var body) && !string.IsNullOrEmpty(body))
    {
        state = ParseStateFromBody(body);
    }
    else if (args.TryGetValue("--releases-json", out var releasesJson) && !string.IsNullOrEmpty(releasesJson))
    {
        state = InitializeFromReleases(releasesJson);
    }
    else
    {
        state = new VersionState("0.0.1", "pre", 1, 0, "none");
    }

    Console.WriteLine(JsonSerializer.Serialize(state, jsonOptions));
    return 0;
}

int HandleCalculate(Dictionary<string, string?> args)
{
    if (!args.TryGetValue("--state", out var stateJson) || string.IsNullOrEmpty(stateJson))
    {
        return Error("--state is required");
    }

    var state = JsonSerializer.Deserialize<VersionState>(stateJson, jsonOptions)
        ?? throw new ArgumentException("Invalid state JSON");

    var versions = CalculateVersions(state);
    Console.WriteLine(JsonSerializer.Serialize(versions, jsonOptions));
    return 0;
}

int HandleAdvance(Dictionary<string, string?> args)
{
    if (!args.TryGetValue("--state", out var stateJson) || string.IsNullOrEmpty(stateJson))
    {
        return Error("--state is required");
    }
    if (!args.TryGetValue("--shipped-version", out var shippedVersion) || string.IsNullOrEmpty(shippedVersion))
    {
        return Error("--shipped-version is required");
    }

    var state = JsonSerializer.Deserialize<VersionState>(stateJson, jsonOptions)
        ?? throw new ArgumentException("Invalid state JSON");

    var newState = AdvanceState(state, shippedVersion);
    Console.WriteLine(JsonSerializer.Serialize(newState, jsonOptions));
    return 0;
}

int HandleBump(Dictionary<string, string?> args)
{
    if (!args.TryGetValue("--state", out var stateJson) || string.IsNullOrEmpty(stateJson))
    {
        return Error("--state is required");
    }
    if (!args.TryGetValue("--version-bump", out var versionBump) || string.IsNullOrEmpty(versionBump))
    {
        return Error("--version-bump is required");
    }
    if (!args.TryGetValue("--phase", out var phase) || string.IsNullOrEmpty(phase))
    {
        return Error("--phase is required");
    }

    args.TryGetValue("--releases-json", out var releasesJson);

    var state = JsonSerializer.Deserialize<VersionState>(stateJson, jsonOptions)
        ?? throw new ArgumentException("Invalid state JSON");

    var result = BumpVersion(state, versionBump, phase, releasesJson);

    if (!result.Valid)
    {
        Console.Error.WriteLine($"Error: {result.Reason}");
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(result.NewState, jsonOptions));
    return 0;
}

int HandleValidate(Dictionary<string, string?> args)
{
    if (!args.TryGetValue("--version", out var version) || string.IsNullOrEmpty(version))
    {
        return Error("--version is required");
    }

    args.TryGetValue("--releases-json", out var releasesJson);

    var result = ValidateVersion(version, releasesJson);
    Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));

    return result.Valid ? 0 : 1;
}

int PrintHelp()
{
    Console.WriteLine("""
        Version management tool for dotnet-dev-certs-plus

        Usage: dotnet scripts/version.cs -- <command> [options]

        Commands:
          state       Read version state from dev draft release
          calculate   Calculate dev and RC versions from state
          advance     Calculate next state after a release is shipped
          bump        Apply version bump and/or phase change
          validate    Check if a version would be valid

        Options for 'state':
          --body <text>           Release body content to parse
          --releases-json <json>  JSON array of releases for initialization

        Options for 'calculate':
          --state <json>          Version state as JSON (required)

        Options for 'advance':
          --state <json>          Current version state as JSON (required)
          --shipped-version <v>   The version that was just shipped (required)

        Options for 'bump':
          --state <json>          Current version state as JSON (required)
          --version-bump <type>   none, auto, patch, minor, major (required)
          --phase <phase>         pre, rc, rtm (required)
          --releases-json <json>  JSON array of releases for validation

        Options for 'validate':
          --version <version>     Version to validate (required)
          --releases-json <json>  JSON array of shipped releases
        """);
    return 0;
}

int Error(string message)
{
    Console.Error.WriteLine($"Error: {message}");
    return 1;
}

Dictionary<string, string?> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string?>();
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].StartsWith("--"))
        {
            var key = args[i];
            string? value = null;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                value = args[++i];
            }
            result[key] = value;
        }
    }
    return result;
}

// === LOCAL FUNCTIONS ===

VersionState ParseStateFromBody(string body)
{
    // Format: <!-- VERSION_STATE: base|phase|phase_number|dev_number|pending -->
    var match = Regex.Match(body, @"<!--\s*VERSION_STATE:\s*([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|([^>\s]+)\s*-->");

    if (!match.Success)
    {
        // Try old format: <!-- VERSION_STATE: base|stage|pre_number|dev_number|pending -->
        match = Regex.Match(body, @"<!--\s*VERSION_STATE:\s*([^|]+)\|([^|]+)\|([^|]+)\|([^|]+)\|?([^>\s]*)\s*-->");

        if (!match.Success)
        {
            throw new ArgumentException("Could not parse VERSION_STATE from release body");
        }
    }

    var baseVersion = match.Groups[1].Value.Trim();
    var phase = match.Groups[2].Value.Trim();
    var phaseNumber = int.Parse(match.Groups[3].Value.Trim());
    var devNumber = int.Parse(match.Groups[4].Value.Trim());
    var pending = match.Groups[5].Value.Trim();

    if (string.IsNullOrEmpty(pending))
    {
        pending = "none";
    }

    // Normalize old "stage" names to new "phase" names
    if (phase == "rtm")
    {
        phaseNumber = 0; // RTM doesn't use phase numbers
    }

    return new VersionState(baseVersion, phase, phaseNumber, devNumber, pending);
}

VersionState InitializeFromReleases(string releasesJson)
{
    var releases = JsonSerializer.Deserialize<List<ReleaseInfo>>(releasesJson, jsonOptions)
        ?? [];

    // Find latest stable release
    var latestStable = releases
        .Where(r => !r.IsDraft && !r.IsPrerelease)
        .Select(r => r.TagName.TrimStart('v'))
        .Where(v => NuGetVersion.TryParse(v, out _))
        .Select(v => NuGetVersion.Parse(v))
        .OrderByDescending(v => v)
        .FirstOrDefault();

    if (latestStable != null)
    {
        // Bump for next version
        int major = latestStable.Major;
        int minor = latestStable.Minor;
        int patch = latestStable.Patch;

        if (major == 0)
        {
            patch++;
        }
        else
        {
            minor++;
            patch = 0;
        }

        return new VersionState($"{major}.{minor}.{patch}", "pre", 1, 0, "none");
    }

    // Default
    return new VersionState("0.0.1", "pre", 1, 0, "none");
}

CalculatedVersions CalculateVersions(VersionState state)
{
    var baseVersion = state.Base;
    var phase = state.Phase;
    var phaseNumber = state.PhaseNumber;
    var devNumber = state.DevNumber + 1; // Increment for next dev build

    string devVersion;
    string rcVersion;

    switch (phase)
    {
        case "pre":
            devVersion = $"{baseVersion}-pre.{phaseNumber}.dev.{devNumber}";
            rcVersion = $"{baseVersion}-pre.{phaseNumber}.rel";
            break;
        case "rc":
            devVersion = $"{baseVersion}-rc.{phaseNumber}.dev.{devNumber}";
            rcVersion = $"{baseVersion}-rc.{phaseNumber}.rel";
            break;
        case "rtm":
            devVersion = $"{baseVersion}-rtm.dev.{devNumber}";
            rcVersion = baseVersion; // Stable version
            break;
        default:
            throw new ArgumentException($"Unknown phase: {phase}");
    }

    // Next state after this dev build
    var nextState = $"{baseVersion}|{phase}|{phaseNumber}|{devNumber}|none";

    return new CalculatedVersions(devVersion, rcVersion, nextState);
}

VersionState AdvanceState(VersionState state, string shippedVersion)
{
    var phase = state.Phase;

    switch (phase)
    {
        case "pre":
        case "rc":
            // Increment phase number, reset dev to 0
            return new VersionState(
                state.Base,
                state.Phase,
                state.PhaseNumber + 1,
                0,
                "none"
            );

        case "rtm":
            // Bump base version, reset to pre.1
            var parts = state.Base.Split('.');
            int major = int.Parse(parts[0]);
            int minor = int.Parse(parts[1]);
            int patch = int.Parse(parts[2]);

            if (major == 0)
            {
                patch++;
            }
            else
            {
                minor++;
                patch = 0;
            }

            return new VersionState(
                $"{major}.{minor}.{patch}",
                "pre",
                1,
                0,
                "none"
            );

        default:
            throw new ArgumentException($"Unknown phase: {phase}");
    }
}

BumpResult BumpVersion(VersionState state, string versionBump, string targetPhase, string? releasesJson)
{
    // Parse current base version
    var parts = state.Base.Split('.');
    int major = int.Parse(parts[0]);
    int minor = int.Parse(parts[1]);
    int patch = int.Parse(parts[2]);

    // Apply version bump
    switch (versionBump.ToLower())
    {
        case "none":
            // No change to version
            break;
        case "auto":
            // Auto bump based on current major
            if (major == 0)
            {
                patch++;
            }
            else
            {
                minor++;
                patch = 0;
            }
            break;
        case "patch":
            patch++;
            break;
        case "minor":
            minor++;
            patch = 0;
            break;
        case "major":
            major++;
            minor = 0;
            patch = 0;
            break;
        default:
            return new BumpResult(false, $"Unknown version bump type: {versionBump}", null);
    }

    // Validate target phase
    if (targetPhase != "pre" && targetPhase != "rc" && targetPhase != "rtm")
    {
        return new BumpResult(false, $"Unknown phase: {targetPhase}. Must be 'pre', 'rc', or 'rtm'.", null);
    }

    var newBaseVersion = $"{major}.{minor}.{patch}";

    // Determine phase number
    int newPhaseNumber = targetPhase == "rtm" ? 0 : 1;

    // If same base version and same phase, this might be invalid
    if (newBaseVersion == state.Base && versionBump == "none")
    {
        // Check if phase transition is valid (can't go backwards)
        var phaseOrder = new Dictionary<string, int> { { "pre", 0 }, { "rc", 1 }, { "rtm", 2 } };

        if (phaseOrder[targetPhase] < phaseOrder[state.Phase])
        {
            return new BumpResult(false,
                $"Cannot move from phase '{state.Phase}' to '{targetPhase}' without bumping version. " +
                $"Phase transitions must move forward (pre → rc → rtm) or include a version bump.",
                null);
        }

        if (targetPhase == state.Phase)
        {
            return new BumpResult(false,
                $"No change requested. Current phase is already '{state.Phase}' and no version bump specified.",
                null);
        }
    }

    // Calculate what the RC version would be
    string proposedRcVersion = targetPhase == "rtm"
        ? newBaseVersion
        : $"{newBaseVersion}-{targetPhase}.1.rel";

    // Validate against shipped releases if provided
    if (!string.IsNullOrEmpty(releasesJson))
    {
        var validation = ValidateVersion(proposedRcVersion, releasesJson);
        if (!validation.Valid)
        {
            return new BumpResult(false, validation.Reason, null);
        }
    }

    var newState = new VersionState(
        newBaseVersion,
        targetPhase,
        newPhaseNumber,
        0, // Reset dev number
        "none"
    );

    return new BumpResult(true, null, newState);
}

ValidationResult ValidateVersion(string version, string? releasesJson)
{
    if (!NuGetVersion.TryParse(version, out var proposedVersion))
    {
        return new ValidationResult(false, $"Invalid version format: {version}");
    }

    if (string.IsNullOrEmpty(releasesJson))
    {
        return new ValidationResult(true, null);
    }

    var releases = JsonSerializer.Deserialize<List<ReleaseInfo>>(releasesJson, jsonOptions)
        ?? [];

    // Get all shipped versions (non-draft)
    var shippedVersions = releases
        .Where(r => !r.IsDraft)
        .Select(r => r.TagName.TrimStart('v'))
        .Where(v => NuGetVersion.TryParse(v, out _))
        .Select(v => NuGetVersion.Parse(v))
        .ToList();

    // Check if proposed version would be going backwards or duplicate
    foreach (var shipped in shippedVersions)
    {
        if (proposedVersion <= shipped)
        {
            return new ValidationResult(false,
                $"Version {version} would not be greater than already shipped version {shipped}. " +
                $"Cannot ship a version that is less than or equal to an existing release.");
        }
    }

    return new ValidationResult(true, null);
}

// === TYPES (records as classes for file-based app compatibility) ===

record VersionState(
    [property: JsonPropertyName("base")] string Base,
    [property: JsonPropertyName("phase")] string Phase,
    [property: JsonPropertyName("phaseNumber")] int PhaseNumber,
    [property: JsonPropertyName("devNumber")] int DevNumber,
    [property: JsonPropertyName("pending")] string Pending
);

record CalculatedVersions(
    [property: JsonPropertyName("devVersion")] string DevVersion,
    [property: JsonPropertyName("rcVersion")] string RcVersion,
    [property: JsonPropertyName("nextState")] string NextState
);

record BumpResult(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("newState")] VersionState? NewState
);

record ValidationResult(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("reason")] string? Reason
);

record ReleaseInfo(
    [property: JsonPropertyName("tagName")] string TagName,
    [property: JsonPropertyName("isDraft")] bool IsDraft,
    [property: JsonPropertyName("isPrerelease")] bool IsPrerelease
);
