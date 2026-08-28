using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CdcModels.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using cdc_api.Controllers.Factory;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Xunit;

namespace cdc_api.Tests.Factory;

public class FactoryControllerTests
{
    private readonly Mock<IDatabaseFactory> _factoryMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly FactoryController _controller;

    public FactoryControllerTests()
    {
        _controller = new FactoryController(
            _factoryMock.Object,
            _orderRepositoryMock.Object,
            NullLogger<FactoryController>.Instance);
    }

    private static Order MakeOrder(
        string status = nameof(OrderStatus.Delivered),
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TemplateId = Guid.NewGuid(),
        TargetConnectionId = Guid.NewGuid(),
        TargetDatabaseName = "acme_test_db",
        Status = status,
        CreatedAt = DateTime.UtcNow,
        StartedAt = DateTime.UtcNow,
        CompletedAt = status == nameof(OrderStatus.Delivered) ? DateTime.UtcNow : null,
        ScriptGroupIds = new[] { Guid.NewGuid() },
        Parameters = new Dictionary<string, object?> { ["key"] = "value" }
    };

    private static CreateOrderDto MakeCreateDto() => new()
    {
        TemplateId = Guid.NewGuid(),
        TargetDatabaseName = "acme_{date}",
        ScriptGroupIds = new[] { Guid.NewGuid() },
        Parameters = new Dictionary<string, object?> { ["Industry"] = "HD" }
    };

    // ───────────────────────────────────────────────────────────────
    // POST /api/factory/orders — Create
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ReturnsCreated_WhenOrderSucceeds()
    {
        var order = MakeOrder();
        _factoryMock.Setup(f => f.OrderAsync(It.IsAny<OrderRequest>()))
            .ReturnsAsync(order);

        var result = await _controller.Create(MakeCreateDto());

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = createdResult.Value.Should().BeOfType<OrderDto>().Subject;
        dto.Id.Should().Be(order.Id);
        dto.Status.Should().Be(nameof(OrderStatus.Delivered));
        dto.TargetDatabaseName.Should().Be("acme_test_db");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenOrderFails()
    {
        var order = MakeOrder(status: nameof(OrderStatus.Failed));
        order.ErrorMessage = "Restore failed: disk full";
        _factoryMock.Setup(f => f.OrderAsync(It.IsAny<OrderRequest>()))
            .ReturnsAsync(order);

        var result = await _controller.Create(MakeCreateDto());

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var dto = badRequest.Value.Should().BeOfType<OrderDto>().Subject;
        dto.Status.Should().Be(nameof(OrderStatus.Failed));
        dto.ErrorMessage.Should().Contain("disk full");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTemplateIdIsEmpty()
    {
        var dto = MakeCreateDto();
        dto.TemplateId = Guid.Empty;

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTargetDatabaseNameIsEmpty()
    {
        var dto = MakeCreateDto();
        dto.TargetDatabaseName = "";

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_PassesAllFieldsToFactory()
    {
        var dto = MakeCreateDto();
        dto.ParameterFilePath = "/params.json";
        OrderRequest? capturedRequest = null;
        _factoryMock.Setup(f => f.OrderAsync(It.IsAny<OrderRequest>()))
            .Callback<OrderRequest>(r => capturedRequest = r)
            .ReturnsAsync(MakeOrder());

        await _controller.Create(dto);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.TemplateId.Should().Be(dto.TemplateId);
        capturedRequest.TargetConnectionId.Should().Be(dto.TargetConnectionId);
        capturedRequest.TargetDatabaseName.Should().Be(dto.TargetDatabaseName);
        capturedRequest.ScriptGroupIds.Should().BeEquivalentTo(dto.ScriptGroupIds);
        capturedRequest.ParameterFilePath.Should().Be("/params.json");
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenArgumentExceptionThrown()
    {
        _factoryMock.Setup(f => f.OrderAsync(It.IsAny<OrderRequest>()))
            .ThrowsAsync(new ArgumentException("Bad request"));

        var result = await _controller.Create(MakeCreateDto());

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ───────────────────────────────────────────────────────────────
    // GET /api/factory/orders — List
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsAllOrders()
    {
        var orders = new List<Order>
        {
            MakeOrder(id: Guid.NewGuid()),
            MakeOrder(id: Guid.NewGuid()),
            MakeOrder(status: nameof(OrderStatus.Failed), id: Guid.NewGuid())
        };
        _orderRepositoryMock.Setup(r => r.ListAsync()).ReturnsAsync(orders);

        var result = await _controller.List();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IReadOnlyList<OrderDto>>().Subject;
        dtos.Should().HaveCount(3);
    }

    [Fact]
    public async Task List_ReturnsEmptyList_WhenNoOrders()
    {
        _orderRepositoryMock.Setup(r => r.ListAsync())
            .ReturnsAsync(new List<Order>());

        var result = await _controller.List();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IReadOnlyList<OrderDto>>().Subject;
        dtos.Should().BeEmpty();
    }

    // ───────────────────────────────────────────────────────────────
    // GET /api/factory/orders/{id} — GetById
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var order = MakeOrder(id: Guid.NewGuid());
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await _controller.GetById(order.Id);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<OrderDto>().Subject;
        dto.Id.Should().Be(order.Id);
        dto.Status.Should().Be(order.Status);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Order?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    // ───────────────────────────────────────────────────────────────
    // GET /api/factory/orders/{id}/status — GetStatus
    // ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ReturnsOk_WhenFound()
    {
        var order = MakeOrder(id: Guid.NewGuid());
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await _controller.GetStatus(order.Id);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<OrderStatusDto>().Subject;
        dto.Id.Should().Be(order.Id);
        dto.Status.Should().Be(order.Status);
        dto.DatabaseName.Should().Be(order.TargetDatabaseName);
    }

    [Fact]
    public async Task GetStatus_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Order?)null);

        var result = await _controller.GetStatus(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetStatus_ReturnsMinimalDto()
    {
        var order = MakeOrder(id: Guid.NewGuid());
        order.ErrorMessage = "Some long error message that should not be in status";
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id)).ReturnsAsync(order);

        var result = await _controller.GetStatus(order.Id);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<OrderStatusDto>().Subject;
        // Status DTO should only have Id, Status, DatabaseName
        dto.Status.Should().Be(order.Status);
        dto.DatabaseName.Should().Be(order.TargetDatabaseName);
    }
}
