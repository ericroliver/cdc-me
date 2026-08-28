using System;
using System.Collections.Generic;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// A provisioned database tracked in the registry for audit.
/// </summary>
public class ProvisionedDatabase
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public Guid ConnectionId { get; set; }
    public Guid TemplateId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? DecommissionedAt { get; set; }
}
