using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Models;
using Softbase.Cdc.Trace;

namespace cdc_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SnapshotController : ControllerBase
{
    private readonly ILogger<SnapshotController> _logger;
    private readonly ISnapshotManager _snapshotManager;

    public SnapshotController(ILogger<SnapshotController> logger, ISnapshotManager snapshotManager)
    {
        _logger = logger;
        _snapshotManager = snapshotManager;
    }

    /// <summary>
    /// Create a new database snapshot
    /// </summary>
    /// <param name="request">Snapshot creation request</param>
    /// <returns>Snapshot creation result</returns>
    [HttpPost]
    public async Task<ActionResult<SnapshotApiResult>> CreateSnapshot([FromBody] CreateSnapshotRequest request)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.DatabaseName) ||
            string.IsNullOrWhiteSpace(request.SnapshotName))
        {
            return BadRequest(new SnapshotApiResult
            {
                Success = false,
                Message = "Required fields are missing: DatabaseName and SnapshotName are required.",
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName
            });
        }

        try
        {
            _logger.LogInformation("Creating snapshot {SnapshotName} for database {DatabaseName}",
                request.SnapshotName, request.DatabaseName);

            var result = await _snapshotManager.CreateSnapshotAsync(
                request.DatabaseName,
                request.SnapshotName);

            return Ok(new SnapshotApiResult
            {
                Success = result.Success,
                Message = result.Message,
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating snapshot {SnapshotName}", request.SnapshotName);
            return BadRequest(new SnapshotApiResult
            {
                Success = false,
                Message = $"Error creating snapshot: {ex.Message}",
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName
            });
        }
    }

    /// <summary>
    /// Restore a database from snapshot
    /// </summary>
    /// <param name="request">Snapshot restore request</param>
    /// <returns>Snapshot restore result</returns>
    [HttpPost("restore")]
    public async Task<ActionResult<SnapshotApiResult>> RestoreSnapshot([FromBody] RestoreSnapshotRequest request)
    {
        try
        {
            _logger.LogInformation("Restoring snapshot {SnapshotName} to database {DatabaseName}",
                request.SnapshotName, request.DatabaseName);

            var result = await _snapshotManager.RestoreSnapshotAsync(
                request.SnapshotName,
                request.DatabaseName);

            return Ok(new SnapshotApiResult
            {
                Success = result.Success,
                Message = result.Message,
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName,
                RestoredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring snapshot {SnapshotName}", request.SnapshotName);
            return BadRequest(new SnapshotApiResult
            {
                Success = false,
                Message = $"Error restoring snapshot: {ex.Message}",
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName
            });
        }
    }

    /// <summary>
    /// List all snapshots for a database
    /// </summary>
    /// <param name="databaseName">Database name</param>
    /// <returns>List of snapshots</returns>
    [HttpGet("{databaseName}/snapshots")]
    public async Task<ActionResult<List<SnapshotInfo>>> ListSnapshots(
        string databaseName)
    {
        try
        {
            _logger.LogInformation("Listing snapshots for database {DatabaseName}", databaseName);

            var snapshots = await _snapshotManager.ListSnapshotsAsync(databaseName);

            return Ok(snapshots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing snapshots for database {DatabaseName}", databaseName);
            return BadRequest(new { error = $"Error listing snapshots: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get snapshot information
    /// </summary>
    /// <param name="databaseName">Database name</param>
    /// <param name="snapshotName">Snapshot name</param>
    /// <returns>Snapshot information</returns>
    [HttpGet("{databaseName}/snapshots/{snapshotName}")]
    public async Task<ActionResult<SnapshotInfo>> GetSnapshotInfo(
        string databaseName,
        string snapshotName)
    {
        try
        {
            _logger.LogInformation("Getting info for snapshot {SnapshotName} of database {DatabaseName}",
                snapshotName, databaseName);

            var snapshots = await _snapshotManager.ListSnapshotsAsync(databaseName);
            var snapshot = snapshots.FirstOrDefault(s => s.SnapshotName == snapshotName);

            if (snapshot == null)
            {
                return NotFound(new { error = $"Snapshot '{snapshotName}' not found" });
            }

            return Ok(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting snapshot info for {SnapshotName}", snapshotName);
            return BadRequest(new { error = $"Error getting snapshot info: {ex.Message}" });
        }
    }

    /// <summary>
    /// Delete a snapshot
    /// </summary>
    /// <param name="snapshotName">Name of the snapshot to delete</param>
    /// <returns>Snapshot deletion result</returns>
    [HttpDelete("{snapshotName}")]
    public async Task<ActionResult<SnapshotApiResult>> DeleteSnapshot(string snapshotName)
    {
        if (string.IsNullOrWhiteSpace(snapshotName))
        {
            return BadRequest("Snapshot name is required");
        }

        try
        {
            _logger.LogInformation("Deleting snapshot {SnapshotName}", snapshotName);

            var result = await _snapshotManager.DropSnapshotAsync(snapshotName);

            return Ok(new SnapshotApiResult
            {
                Success = result.Success,
                Message = result.Message,
                SnapshotName = snapshotName,
                DeletedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snapshot {SnapshotName}", snapshotName);
            return BadRequest(new SnapshotApiResult
            {
                Success = false,
                Message = $"Error deleting snapshot: {ex.Message}",
                SnapshotName = snapshotName
            });
        }
    }
}

// API Request/Response Models
public class CreateSnapshotRequest
{
    public string DatabaseName { get; set; } = string.Empty;
    public string SnapshotName { get; set; } = string.Empty;
}

public class RestoreSnapshotRequest
{
    public string DatabaseName { get; set; } = string.Empty;
    public string SnapshotName { get; set; } = string.Empty;
}

public class DeleteSnapshotRequest
{
    public string SnapshotName { get; set; } = string.Empty;
}

public class SnapshotApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SnapshotName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? RestoredAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
