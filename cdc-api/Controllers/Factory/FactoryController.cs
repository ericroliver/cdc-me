using CdcModels.Factory;
using Microsoft.AspNetCore.Mvc;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace cdc_api.Controllers.Factory;

/// <summary>
/// Order API — the user-facing entry point for the factory system.
/// Creates, lists, and inspects provisioning orders.
/// </summary>
[ApiController]
[Route("api/factory/orders")]
public class FactoryController : ControllerBase
{
    private readonly IDatabaseFactory _factory;
    private readonly IOrderRepository _orderRepository;
    private readonly IDatabaseTemplateRepository _templateRepository;
    private readonly ILogger<FactoryController> _logger;

    public FactoryController(
        IDatabaseFactory factory,
        IOrderRepository orderRepository,
        IDatabaseTemplateRepository templateRepository,
        ILogger<FactoryController> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _templateRepository = templateRepository ?? throw new ArgumentNullException(nameof(templateRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new provisioning order.
    /// The order is processed synchronously and the result (with final status) is returned.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (request.TemplateId == Guid.Empty)
            return BadRequest(new { error = "TemplateId is required." });

        if (string.IsNullOrWhiteSpace(request.TargetDatabaseName))
            return BadRequest(new { error = "TargetDatabaseName is required." });

        // Validate template exists before creating the order
        var template = await _templateRepository.GetByIdAsync(request.TemplateId);
        if (template is null)
            return NotFound(new { error = $"Template not found: {request.TemplateId}" });

        try
        {
            var orderRequest = new OrderRequest
            {
                TemplateId = request.TemplateId,
                TargetConnectionId = request.TargetConnectionId,
                TargetDatabaseName = request.TargetDatabaseName,
                ScriptGroupIds = request.ScriptGroupIds,
                Parameters = request.Parameters,
                ParameterFilePath = request.ParameterFilePath
            };

            _logger.LogInformation("Creating order for template {TemplateId} -> database '{DbName}'", request.TemplateId, request.TargetDatabaseName);

            var order = await _factory.OrderAsync(orderRequest);
            var dto = MapToDto(order);

            if (order.Status == nameof(OrderStatus.Failed))
            {
                _logger.LogWarning("Order {OrderId} failed: {Error}", order.Id, order.ErrorMessage);
                return BadRequest(dto);
            }

            _logger.LogInformation("Order {OrderId} completed with status {Status}", order.Id, order.Status);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid order request for template {TemplateId}", request.TemplateId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all orders.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> List()
    {
        var orders = await _orderRepository.ListAsync();
        return Ok(orders.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get full order details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order is null)
            return NotFound();

        return Ok(MapToDto(order));
    }

    /// <summary>
    /// Lightweight status polling — returns just the status and database name.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<OrderStatusDto>> GetStatus(Guid id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        if (order is null)
            return NotFound();

        return Ok(new OrderStatusDto
        {
            Id = order.Id,
            Status = order.Status,
            DatabaseName = order.TargetDatabaseName
        });
    }

    private static OrderDto MapToDto(Order order) => new()
    {
        Id = order.Id,
        TemplateId = order.TemplateId,
        TargetConnectionId = order.TargetConnectionId,
        TargetDatabaseName = order.TargetDatabaseName,
        Status = order.Status,
        ErrorMessage = order.ErrorMessage,
        CreatedAt = order.CreatedAt,
        StartedAt = order.StartedAt,
        CompletedAt = order.CompletedAt,
        ScriptGroupIds = order.ScriptGroupIds,
        Parameters = order.Parameters.ToDictionary(p => p.Key, p => p.Value)
    };
}
