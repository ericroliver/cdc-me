using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

public interface IDatabaseTemplateRepository
{
    Task<Template?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<Template>> ListAsync();
    Task<Template> RegisterAsync(RegisterTemplateRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> VerifyAsync(Guid id);
}

public class RegisterTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Platform { get; set; } = "SqlServer";
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Checksum { get; set; }
    public string? CreatedBy { get; set; }
}
