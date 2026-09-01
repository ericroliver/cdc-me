using System.ComponentModel.DataAnnotations;

namespace CdcModels.Factory;

/// <summary>
/// Request body for creating a new factory order.
/// </summary>
public class CreateOrderDto
{
    [Required]
    public Guid TemplateId { get; set; }

    public Guid? TargetConnectionId { get; set; }

    /// <summary>
    /// Optional: reference a connection by name instead of ID.
    /// Used only when <see cref="TargetConnectionId"/> is not specified.
    /// </summary>
    public string? TargetConnectionName { get; set; }

    [Required]
    public string TargetDatabaseName { get; set; } = string.Empty;

    public IReadOnlyList<Guid> ScriptGroupIds { get; set; } = Array.Empty<Guid>();

    public Dictionary<string, object?>? Parameters { get; set; }

    public string? ParameterFilePath { get; set; }
}
