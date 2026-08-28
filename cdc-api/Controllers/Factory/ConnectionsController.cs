using CdcModels.Factory;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Factory.Engine;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace cdc_api.Controllers.Factory;

/// <summary>
/// Manages the central registry of named database server connections.
/// Everything else (templates, orders, provisioned databases) references
/// connections by ID rather than embedding raw connection strings.
/// </summary>
[ApiController]
[Route("api/factory/connections")]
public class ConnectionsController : ControllerBase
{
    private readonly IConnectionRegistry _connectionRegistry;
    private readonly ILogger<ConnectionsController> _logger;

    public ConnectionsController(
        IConnectionRegistry connectionRegistry,
        ILogger<ConnectionsController> logger)
    {
        _connectionRegistry = connectionRegistry ?? throw new ArgumentNullException(nameof(connectionRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register a new connection.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConnectionDto>> Create([FromBody] CreateConnectionDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            _logger.LogInformation("Creating connection '{Name}' ({Platform})", request.Name, request.Platform);
            var created = await _connectionRegistry.CreateAsync(new CreateConnectionRequest
            {
                Name = request.Name,
                Platform = request.Platform,
                Host = request.Host,
                Port = request.Port,
                ConnectionString = request.ConnectionString,
                Description = request.Description,
                IsDefault = request.IsDefault
            });

            var dto = MapToDto(created);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid connection request for '{Name}'", request.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all registered connections.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectionDto>>> List()
    {
        var connections = await _connectionRegistry.ListAsync();
        return Ok(connections.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get a specific connection by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConnectionDto>> GetById(Guid id)
    {
        var connection = await _connectionRegistry.GetByIdAsync(id);
        if (connection is null)
            return NotFound();

        return Ok(MapToDto(connection));
    }

    /// <summary>
    /// Update an existing connection.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConnectionDto>> Update(Guid id, [FromBody] UpdateConnectionDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var updated = await _connectionRegistry.UpdateAsync(id, new UpdateConnectionRequest
        {
            Host = request.Host,
            Port = request.Port,
            ConnectionString = request.ConnectionString,
            Description = request.Description,
            IsDefault = request.IsDefault
        });

        if (updated is null)
            return NotFound();

        return Ok(MapToDto(updated));
    }

    /// <summary>
    /// Delete a connection.
    /// Returns 409 Conflict if the connection is referenced by existing orders.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _connectionRegistry.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (ReferencedByOrdersException)
        {
            return Conflict(new { error = "Cannot delete connection referenced by existing orders." });
        }
    }

    /// <summary>
    /// Test (ping) a connection to verify it is reachable.
    /// </summary>
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult> Test(Guid id)
    {
        var exists = await _connectionRegistry.GetByIdAsync(id);
        if (exists is null)
            return NotFound();

        var success = await _connectionRegistry.TestConnectionAsync(id);
        if (success)
            return Ok(new { success = true, message = "Connection successful" });

        return Ok(new { success = false, message = "Connection failed" });
    }

    private static ConnectionDto MapToDto(Connection connection) => new()
    {
        Id = connection.Id,
        Name = connection.Name,
        Platform = connection.Platform,
        Host = connection.Host,
        Port = connection.Port,
        ConnectionString = connection.ConnectionString,
        Description = connection.Description,
        IsDefault = connection.IsDefault,
        CreatedAt = connection.CreatedAt,
        UpdatedAt = connection.UpdatedAt
    };
}
