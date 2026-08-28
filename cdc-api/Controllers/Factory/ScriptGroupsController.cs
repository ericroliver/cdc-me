using CdcModels.Factory;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace cdc_api.Controllers.Factory;

[ApiController]
[Route("api/factory/script-groups")]
public class ScriptGroupsController : ControllerBase
{
    private readonly IScriptGroupRepository _repository;
    private readonly IScriptLibrary _scriptLibrary;
    private readonly ILogger<ScriptGroupsController> _logger;

    public ScriptGroupsController(
        IScriptGroupRepository repository,
        IScriptLibrary scriptLibrary,
        ILogger<ScriptGroupsController> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _scriptLibrary = scriptLibrary ?? throw new ArgumentNullException(nameof(scriptLibrary));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<ActionResult<ScriptGroupDto>> Create([FromBody] CreateScriptGroupDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _repository.CreateGroupAsync(new CreateScriptGroupRequest
            {
                Name = request.Name,
                Description = request.Description,
                Layer = request.Layer,
                Order = request.Order,
                Dependencies = request.Dependencies
            });

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScriptGroupDto>>> List([FromQuery] int? layer)
    {
        var groups = await _repository.ListGroupsAsync(layer);
        return Ok(groups.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScriptGroupDto>> GetById(Guid id)
    {
        var group = await _repository.GetGroupAsync(id);
        if (group is null)
            return NotFound();

        var dto = MapToDto(group);
        // Include scripts in the group
        var scripts = await _scriptLibrary.ListScriptsAsync(id);
        dto.Scripts = scripts.Select(MapScriptToDto).ToList();

        return Ok(dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScriptGroupDto>> Update(Guid id, [FromBody] UpdateScriptGroupDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var updated = await _repository.UpdateGroupAsync(id, new UpdateScriptGroupRequest
        {
            Name = request.Name,
            Description = request.Description,
            Layer = request.Layer,
            Order = request.Order,
            Dependencies = request.Dependencies
        });

        if (updated is null)
            return NotFound();

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _repository.DeleteGroupAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private static ScriptGroupDto MapToDto(ScriptGroup group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        Description = group.Description,
        Layer = group.Layer,
        Order = group.Order,
        Dependencies = group.Dependencies.ToList(),
        CreatedAt = group.CreatedAt,
        UpdatedAt = group.UpdatedAt
    };

    private static ScriptDto MapScriptToDto(Script script) => new()
    {
        Id = script.Id,
        Name = script.Name,
        Description = script.Description,
        Type = script.Type,
        Content = script.Content,
        FilePath = script.FilePath,
        ScriptGroupId = script.ScriptGroupId,
        Order = script.Order,
        CreatedAt = script.CreatedAt,
        UpdatedAt = script.UpdatedAt
    };
}
