using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Softbase.Cdc;

namespace cdc_api.HealthChecks;

/// <summary>
/// Health check that includes version information in the response.
/// </summary>
public class VersionHealthCheck : IHealthCheck
{
    private readonly IVersionProvider _versionProvider;

    public VersionHealthCheck(IVersionProvider versionProvider)
    {
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["version"] = _versionProvider.Version,
            ["informationalVersion"] = _versionProvider.InformationalVersion,
            ["commitHash"] = _versionProvider.CommitHash,
            ["buildDate"] = _versionProvider.BuildDate
        };

        return Task.FromResult(HealthCheckResult.Healthy("Healthy", data));
    }
}
