using System;
using System.ComponentModel.DataAnnotations;

namespace CdcModels.Factory;

public class TemplateDto
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

public class CreateTemplateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Version { get; set; } = string.Empty;

    public string Platform { get; set; } = "SqlServer";

    [Required]
    public string FilePath { get; set; } = string.Empty;

    public string? Description { get; set; }
}
