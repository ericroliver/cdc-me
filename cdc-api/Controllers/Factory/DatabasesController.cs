using CdcModels.Factory;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace cdc_api.Controllers.Factory;

/// <summary>
/// Read-only registry of every database DTAI has provisioned.
/// </summary>
[ApiController]
[Route("api/factory/databases")]
public class DatabasesController : ControllerBase
{
    private readonly IDatabaseRegistry _databaseRegistry;
    private readonly ILogger<DatabasesController> _logger;

    public DatabasesController(IDatabaseRegistry databaseRegistry, ILogger<DatabasesController> logger)
    {
        _databaseRegistry = databaseRegistry ?? throw new ArgumentNullException(nameof(databaseRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProvisionedDatabaseDto>>> List()
    {
        _logger.LogInformation("Listing all provisioned databases");
        var databases = await _databaseRegistry.ListAsync();
        return Ok(databases.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProvisionedDatabaseDto>> GetById(Guid id)
    {
        _logger.LogInformation("Getting provisioned database {DatabaseId}", id);
        var database = await _databaseRegistry.GetByIdAsync(id);
        if (database is null)
        {
            _logger.LogWarning("Provisioned database {DatabaseId} not found", id);
            return NotFound();
        }

        return Ok(MapToDto(database));
    }

    private static ProvisionedDatabaseDto MapToDto(ProvisionedDatabase db) => new()
    {
        Id = db.Id,
        OrderId = db.OrderId,
        DatabaseName = db.DatabaseName,
        ConnectionId = db.ConnectionId,
        TemplateId = db.TemplateId,
        Status = db.Status,
        CreatedAt = db.CreatedAt,
        DecommissionedAt = db.DecommissionedAt
    };
}
