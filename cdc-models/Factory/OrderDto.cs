namespace CdcModels.Factory;

/// <summary>
/// Full order representation returned by the API.
/// </summary>
public class OrderDto
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid? TargetConnectionId { get; set; }
    public string TargetDatabaseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public IReadOnlyList<Guid> ScriptGroupIds { get; set; } = Array.Empty<Guid>();
    public Dictionary<string, object?> Parameters { get; set; } = new();
}
