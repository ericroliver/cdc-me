using CdcModels.Factory;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace cdc_api.Controllers.Factory;

[ApiController]
[Route("api/factory/scripts")]
public class ScriptsController : ControllerBase
{
    private readonly IScriptLibrary _scriptLibrary;
    private readonly ILogger<ScriptsController> _logger;

    public ScriptsController(IScriptLibrary scriptLibrary, ILogger<ScriptsController> logger)
    {
        _scriptLibrary = scriptLibrary ?? throw new ArgumentNullException(nameof(scriptLibrary));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public async Task<ActionResult<ScriptDto>> Create([FromBody] CreateScriptDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Content) && string.IsNullOrWhiteSpace(request.FilePath))
            return BadRequest(new { error = "Either Content or FilePath must be provided" });

        if (request.ScriptGroupId == Guid.Empty)
            return BadRequest(new { error = "ScriptGroupId is required and must be a valid GUID." });

        try
        {
            _logger.LogInformation("Creating script '{Name}' in group {ScriptGroupId}", request.Name, request.ScriptGroupId);
            var created = await _scriptLibrary.CreateScriptAsync(new CreateScriptRequest
            {
                Name = request.Name,
                Description = request.Description,
                Type = request.Type,
                Content = request.Content,
                FilePath = request.FilePath,
                ScriptGroupId = request.ScriptGroupId,
                Order = request.Order
            });

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid script request for '{Name}'", request.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScriptDto>>> List([FromQuery] Guid? groupId)
    {
        var scripts = await _scriptLibrary.ListScriptsAsync(groupId);
        return Ok(scripts.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ScriptDto>> GetById(Guid id)
    {
        var script = await _scriptLibrary.GetScriptAsync(id);
        if (script is null)
            return NotFound();

        return Ok(MapToDto(script));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ScriptDto>> Update(Guid id, [FromBody] UpdateScriptDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var updated = await _scriptLibrary.UpdateScriptAsync(id, new UpdateScriptRequest
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Content = request.Content,
            FilePath = request.FilePath,
            Order = request.Order
        });

        if (updated is null)
            return NotFound();

        return Ok(MapToDto(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _scriptLibrary.DeleteScriptAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    private static ScriptDto MapToDto(Script script) => new()
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
