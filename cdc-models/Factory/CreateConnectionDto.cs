using System.ComponentModel.DataAnnotations;

namespace CdcModels.Factory;

/// <summary>
/// Request body for registering a new connection.
/// </summary>
public class CreateConnectionDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string Platform { get; set; } = "SqlServer";

    public string Host { get; set; } = string.Empty;

    public int? Port { get; set; }

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }
}
