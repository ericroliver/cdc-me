using System;
using System.ComponentModel.DataAnnotations;

namespace CdcModels.Factory;

public class ScriptDto
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

public class CreateScriptDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Type { get; set; } = "SqlScript";

    public string? Content { get; set; }

    public string? FilePath { get; set; }

    [Required]
    public Guid ScriptGroupId { get; set; }

    public int Order { get; set; }
}

public class UpdateScriptDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public int? Order { get; set; }
}
