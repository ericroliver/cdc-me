using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// A database backup file registered as a starting point for provisioning.
/// </summary>
public class Template
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Platform { get; set; } = "SqlServer";
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Checksum { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
