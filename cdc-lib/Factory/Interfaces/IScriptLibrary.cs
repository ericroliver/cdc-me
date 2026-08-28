using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

public interface IScriptLibrary
{
    Task<Script?> GetScriptAsync(Guid id);
    Task<IReadOnlyList<Script>> ListScriptsAsync(Guid? groupId = null);
    Task<Script> CreateScriptAsync(CreateScriptRequest request);
    Task<Script?> UpdateScriptAsync(Guid id, UpdateScriptRequest request);
    Task<bool> DeleteScriptAsync(Guid id);
}

public class CreateScriptRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "SqlScript";
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public Guid ScriptGroupId { get; set; }
    public int Order { get; set; }
}

public class UpdateScriptRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Content { get; set; }
    public string? FilePath { get; set; }
    public int? Order { get; set; }
}
