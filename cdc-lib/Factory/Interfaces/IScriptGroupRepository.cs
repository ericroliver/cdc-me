using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

public interface IScriptGroupRepository
{
    Task<ScriptGroup?> GetGroupAsync(Guid id);
    Task<IReadOnlyList<ScriptGroup>> ListGroupsAsync(int? layer = null);
    Task<ScriptGroup> CreateGroupAsync(CreateScriptGroupRequest request);
    Task<ScriptGroup?> UpdateGroupAsync(Guid id, UpdateScriptGroupRequest request);
    Task<bool> DeleteGroupAsync(Guid id);
}

public class CreateScriptGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Layer { get; set; }
    public int Order { get; set; }
    public IReadOnlyList<Guid> Dependencies { get; set; } = Array.Empty<Guid>();
}

public class UpdateScriptGroupRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? Layer { get; set; }
    public int? Order { get; set; }
    public IReadOnlyList<Guid>? Dependencies { get; set; }
}
