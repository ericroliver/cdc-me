using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Softbase.Cdc;

/// <summary>
/// Default implementation that reads version info from assembly metadata
/// and environment variables set during the Docker build.
/// </summary>
public class VersionProvider : IVersionProvider
{
    private readonly string _version;
    private readonly string _informationalVersion;
    private readonly string _commitHash;
    private readonly string _buildDate;

    public VersionProvider()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var informationalAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var versionAttr = assembly.GetCustomAttribute<AssemblyVersionAttribute>();

        _informationalVersion = informationalAttr?.InformationalVersion ?? "0.0.0";
        _version = versionAttr?.Version ?? "0.0.0.0";

        // Parse commit hash from informational version (format: "1.0.0+sha.abc1234")
        // or fall back to environment variable
        _commitHash = ParseCommitHash(_informationalVersion)
            ?? Environment.GetEnvironmentVariable("GIT_COMMIT")
            ?? "unknown";

        _buildDate = Environment.GetEnvironmentVariable("BUILD_DATE")
            ?? File.GetLastWriteTimeUtc(assembly.Location).ToString("O");
    }

    public string Version => _version;
    public string InformationalVersion => _informationalVersion;
    public string CommitHash => _commitHash;
    public string BuildDate => _buildDate;
    public string RuntimeVersion => Environment.Version.ToString();

    private static string? ParseCommitHash(string informationalVersion)
    {
        // Format: "1.0.0-dev.42+sha.abc1234"
        var plusIndex = informationalVersion.IndexOf('+');
        if (plusIndex < 0 || plusIndex >= informationalVersion.Length - 1)
            return null;

        var metadata = informationalVersion[(plusIndex + 1)..];
        if (metadata.StartsWith("sha."))
            return metadata[4..];

        return metadata;
    }
}
