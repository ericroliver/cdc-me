using CdcModels.Factory;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace cdc_api.Controllers.Factory;

[ApiController]
[Route("api/factory/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IDatabaseTemplateRepository _templateRepository;
    private readonly ITemplateStorageProvider _storageProvider;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        IDatabaseTemplateRepository templateRepository,
        ITemplateStorageProvider storageProvider,
        ILogger<TemplatesController> logger)
    {
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("upload")]
    public async Task<ActionResult<TemplateDto>> Upload([FromForm] UploadTemplateDto request)
    {
        if (request.File is null || request.File.Length == 0)
            return BadRequest(new { error = "File is required" });

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required" });

        _logger.LogInformation("Storing template '{Name}' version '{Version}'", request.Name, request.Version);

        // Store the file
        var fileName = $"{request.Name}_{request.Version}_{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
        await using var stream = request.File.OpenReadStream();
        var filePath = await _storageProvider.StoreAsync(stream, fileName);

        // Register the template
        var template = await _templateRepository.RegisterAsync(new RegisterTemplateRequest
        {
            Name = request.Name,
            Version = request.Version ?? "",
            Platform = request.Platform ?? "SqlServer",
            FilePath = filePath,
            Description = request.Description,
            CreatedBy = request.CreatedBy
        });

        return CreatedAtAction(nameof(GetById), new { id = template.Id }, MapToDto(template));
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> RegisterByPath([FromBody] CreateTemplateDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!_storageProvider.Exists(request.FilePath))
            return BadRequest(new { error = $"File not found at path: {request.FilePath}" });

        try
        {
            var template = await _templateRepository.RegisterAsync(new RegisterTemplateRequest
            {
                Name = request.Name,
                Version = request.Version,
                Platform = request.Platform,
                FilePath = request.FilePath,
                Description = request.Description,
                CreatedBy = null
            });

            return CreatedAtAction(nameof(GetById), new { id = template.Id }, MapToDto(template));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid template registration for '{Name}'", request.Name);
            return BadRequest(new { error = ex.Message });
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogWarning(ex, "Template file not found: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List()
    {
        var templates = await _templateRepository.ListAsync();
        return Ok(templates.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateDto>> GetById(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template is null)
            return NotFound();

        return Ok(MapToDto(template));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await _templateRepository.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult> Verify(Guid id)
    {
        var verified = await _templateRepository.VerifyAsync(id);
        if (!verified)
            return Ok(new { success = false, message = "Verification failed — file missing or checksum mismatch" });

        return Ok(new { success = true, message = "File verified successfully" });
    }

    private static TemplateDto MapToDto(Template template) => new()
    {
        Id = template.Id,
        Name = template.Name,
        Version = template.Version,
        Platform = template.Platform,
        FilePath = template.FilePath,
        Description = template.Description,
        Checksum = template.Checksum,
        CreatedAt = template.CreatedAt,
        CreatedBy = template.CreatedBy
    };
}

/// <summary>
/// Upload DTO for multipart form-data template upload.
/// </summary>
public class UploadTemplateDto
{
    public IFormFile? File { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Platform { get; set; }
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
}
