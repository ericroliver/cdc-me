namespace CdcModels;

/// <summary>
/// Version information returned by GET /api/version.
/// </summary>
public class VersionInfoDto
{
    /// <summary>
    /// Semantic version (e.g., "1.0.0").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Full informational version, may include commit hash (e.g., "1.0.0-dev.42+sha.abc1234").
    /// </summary>
    public string InformationalVersion { get; set; } = string.Empty;

    /// <summary>
    /// Git commit hash the build was created from.
    /// </summary>
    public string CommitHash { get; set; } = string.Empty;

    /// <summary>
    /// Build timestamp (UTC ISO 8601).
    /// </summary>
    public string BuildDate { get; set; } = string.Empty;

    /// <summary>
    /// .NET runtime version in use.
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;
}
