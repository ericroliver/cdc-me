using System;
using System.Collections.Generic;

namespace CdcModels.Factory;

public class ScriptGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Layer { get; set; }
    public int Order { get; set; }
    public List<Guid> Dependencies { get; set; } = new();
    public List<ScriptDto> Scripts { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateScriptGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Layer { get; set; }
    public int Order { get; set; }
    public List<Guid> Dependencies { get; set; } = new();
}

public class UpdateScriptGroupDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Layer { get; set; }
    public int? Order { get; set; }
    public List<Guid>? Dependencies { get; set; }
}
