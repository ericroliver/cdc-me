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

            if (!result.Success)
            {
                _logger.LogWarning("Snapshot creation failed for database {DatabaseName}: {Message}", request.DatabaseName, result.Message);
                return BadRequest(new { error = "Failed to create snapshot. Please check server logs for details." });
            }

            return Ok(new SnapshotApiResult
            {
                Success = true,
                Message = result.Message,
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // SECURITY: Log detailed error server-side only, return generic message to client
            _logger.LogError(ex, "Error creating snapshot {SnapshotName} for database {DatabaseName}",
                request.SnapshotName, request.DatabaseName);
            return BadRequest(new SnapshotApiResult
            {
                Success = false,
                Message = "Failed to create snapshot. Please check server logs for details.",
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
            _logger.LogInformation("Restoring snapshot {SnapshotName} to database {DatabaseName}",
                request.SnapshotName, request.DatabaseName);

            var result = await _snapshotManager.RestoreSnapshotAsync(
                request.SnapshotName,
                request.DatabaseName);

            if (!result.Success)
            {
                _logger.LogWarning("Snapshot restore failed for snapshot {SnapshotName}: {Message}", request.SnapshotName, result.Message);
                return BadRequest(new { error = "Failed to restore snapshot. Please check server logs for details." });
            }

            return Ok(new SnapshotApiResult
            {
                Success = true,
                Message = result.Message,
                SnapshotName = request.SnapshotName,
                DatabaseName = request.DatabaseName,
                RestoredAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            // SECURITY: Log detailed error server-side only, return generic message to client
            _logger.LogError(ex, "Error restoring snapshot {SnapshotName} to database {DatabaseName}",
                request.SnapshotName, request.DatabaseName);
            return BadRequest(new SnapshotApiResult
            {
                Success = false,
                Message = "Failed to restore snapshot. Please check server logs for details.",
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
            return BadRequest(new { error = "Failed to list snapshots. Please check server logs for details." });
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
            return BadRequest(new { error = "Failed to get snapshot info. Please check server logs for details." });
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
            return BadRequest(new { error = "Snapshot name is required" });
        }

        try
        {
            _logger.LogInformation("Deleting snapshot {SnapshotName}", snapshotName);

            var result = await _snapshotManager.DropSnapshotAsync(snapshotName);

            if (!result.Success)
            {
                _logger.LogWarning("Snapshot deletion failed for {SnapshotName}: {Message}", snapshotName, result.Message);
                return NotFound(new { error = $"Snapshot '{snapshotName}' not found or could not be deleted." });
            }

            return Ok(new SnapshotApiResult
            {
                Success = true,
                Message = result.Message,
                SnapshotName = snapshotName,
                DeletedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting snapshot {SnapshotName}", snapshotName);
            return BadRequest(new { error = "Failed to delete snapshot. Please check server logs for details." });
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
