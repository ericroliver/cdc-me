using System;
using System.Collections.Generic;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// A logical grouping of scripts that share parameters, ordered within a layer.
/// </summary>
public class ScriptGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Layer { get; set; }
    public int Order { get; set; }
    public IReadOnlyList<Guid> Dependencies { get; set; } = Array.Empty<Guid>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
