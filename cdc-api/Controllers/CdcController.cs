using Microsoft.AspNetCore.Mvc;

namespace cdc_api.Controllers;

[ApiController]
[Route("[controller]")]
public class CdcController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<CdcController> _logger;

    public CdcController(ILogger<CdcController> logger)
    {
        _logger = logger;
    }

    [HttpPost(Name = "resetDatabase")]
    public CdcOperationResult ResetDatabase()
    {
        // first destroy the container
        //  docker container rm 'container_name'
        // stand it back up
        // docker run --name mssqlDb_container-1  -i -d ghcr.io/yaitde-x/sb-sql-tpa:latest 
        var command = "docker container rm 'container_name'";
        return new CdcOperationResult();
    }
}

public class CdcOperationResult
{

}
