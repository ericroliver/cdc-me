namespace Softbase.Cdc;

/// <summary>
/// Provides runtime version information for the application.
/// </summary>
public interface IVersionProvider
{
    /// <summary>
    /// Semantic version string (e.g., "1.0.0", "1.0.0-dev.42").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Full informational version, may include commit hash (e.g., "1.0.0-dev.42+sha.abc1234").
    /// </summary>
    string InformationalVersion { get; }

    /// <summary>
    /// Git commit hash the build was created from, or "unknown".
    /// </summary>
    string CommitHash { get; }

    /// <summary>
    /// Build timestamp (UTC), or "unknown".
    /// </summary>
    string BuildDate { get; }

    /// <summary>
    /// .NET runtime version in use.
    /// </summary>
    string RuntimeVersion { get; }
}
