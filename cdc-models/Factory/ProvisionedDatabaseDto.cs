using System;

namespace CdcModels.Factory;

public class ProvisionedDatabaseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public Guid ConnectionId { get; set; }
    public string? ConnectionName { get; set; }
    public Guid TemplateId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? DecommissionedAt { get; set; }
}
