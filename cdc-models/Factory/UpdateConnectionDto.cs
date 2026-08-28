namespace CdcModels.Factory;

/// <summary>
/// Request body for updating an existing connection.
/// All fields are optional — only provided fields are updated.
/// </summary>
public class UpdateConnectionDto
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? ConnectionString { get; set; }
    public string? Description { get; set; }
    public bool? IsDefault { get; set; }
}
