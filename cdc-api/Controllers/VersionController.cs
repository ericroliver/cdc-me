using CdcModels;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc;

namespace cdc_api.Controllers;

/// <summary>
/// Version information endpoint.
/// </summary>
[ApiController]
[Route("api/version")]
public class VersionController : ControllerBase
{
    private readonly IVersionProvider _versionProvider;

    public VersionController(IVersionProvider versionProvider)
    {
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
    }

    /// <summary>
    /// Returns the running application version, commit hash, and build date.
    /// </summary>
    [HttpGet]
    public ActionResult<VersionInfoDto> Get()
    {
        return Ok(new VersionInfoDto
        {
            Version = _versionProvider.Version,
            InformationalVersion = _versionProvider.InformationalVersion,
            CommitHash = _versionProvider.CommitHash,
            BuildDate = _versionProvider.BuildDate,
            RuntimeVersion = _versionProvider.RuntimeVersion
        });
    }
}
