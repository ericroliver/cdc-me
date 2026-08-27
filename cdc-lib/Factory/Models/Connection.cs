using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// A named, registered database server instance that everything else
/// references by ID. No raw connection strings scattered across orders or scripts.
/// </summary>
public class Connection
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
