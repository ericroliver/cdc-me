using System;
using System.Collections.Generic;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// A single executable unit — in Phase 1, a SQL script.
/// </summary>
public class Script
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "SqlScript";
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public Guid ScriptGroupId { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
