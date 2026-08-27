namespace CdcModels.Factory;

/// <summary>
/// Full connection representation returned by the API.
/// </summary>
public class ConnectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = "SqlServer";
    public string Host { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
