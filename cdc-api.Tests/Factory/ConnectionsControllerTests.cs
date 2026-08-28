using CdcModels.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using cdc_api.Controllers.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

public class ConnectionsControllerTests
{
    private readonly Mock<IConnectionRegistry> _registryMock = new();
    private readonly ILogger<ConnectionsController> _logger = NullLogger<ConnectionsController>.Instance;
    private readonly ConnectionsController _controller;

    public ConnectionsControllerTests()
    {
        _controller = new ConnectionsController(_registryMock.Object, _logger);
    }

    private static Connection MakeConnection(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "dev-sqlserver",
        Platform = "SqlServer",
        Host = "sqlserver",
        Port = 1433,
        ConnectionString = "Server=sqlserver;User Id=sa;Password=Test123!;",
        Description = "Dev SQL Server",
        IsDefault = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Create_ReturnsCreatedAt_WhenValid()
    {
        var dto = new CreateConnectionDto
        {
            Name = "dev-sqlserver",
            Platform = "SqlServer",
            Host = "sqlserver",
            Port = 1433,
            ConnectionString = "Server=sqlserver;",
            IsDefault = true
        };

        var connection = MakeConnection();
        _registryMock.Setup(r => r.CreateAsync(It.IsAny<CreateConnectionRequest>()))
                     .ReturnsAsync(connection);

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var created = result.Result.As<CreatedAtActionResult>().Value.As<ConnectionDto>();
        created.Name.Should().Be("dev-sqlserver");
        created.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameMissing()
    {
        var dto = new CreateConnectionDto { ConnectionString = "cs" };
        _controller.ModelState.AddModelError("Name", "Name is required");

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenArgumentException()
    {
        var dto = new CreateConnectionDto { Name = "test", ConnectionString = "cs" };
        _registryMock.Setup(r => r.CreateAsync(It.IsAny<CreateConnectionRequest>()))
                     .ThrowsAsync(new ArgumentException("Name is required"));

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task List_ReturnsAllConnections()
    {
        var connections = new List<Connection>
        {
            MakeConnection(),
            new Connection
            {
                Id = Guid.NewGuid(),
                Name = "qa-sqlserver",
                Platform = "SqlServer",
                Host = "qa",
                Port = 1433,
                ConnectionString = "Server=qa;",
                Description = "QA SQL Server",
                IsDefault = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        _registryMock.Setup(r => r.ListAsync()).ReturnsAsync(connections);

        var result = await _controller.List();

        result.Result.Should().BeOfType<OkObjectResult>();
        var dtos = result.Result.As<OkObjectResult>().Value.As<List<ConnectionDto>>();
        dtos.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(MakeConnection(id));

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
        var dto = result.Result.As<OkObjectResult>().Value.As<ConnectionDto>();
        dto.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Connection?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_ReturnsOk_WhenFound()
    {
        var id = Guid.NewGuid();
        var dto = new UpdateConnectionDto { Host = "newhost", Port = 1434 };
        _registryMock.Setup(r => r.UpdateAsync(id, It.IsAny<UpdateConnectionRequest>()))
                     .ReturnsAsync(MakeConnection(id));

        var result = await _controller.Update(id, dto);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.UpdateAsync(id, It.IsAny<UpdateConnectionRequest>()))
                     .ReturnsAsync((Connection?)null);

        var result = await _controller.Update(id, new UpdateConnectionDto());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.DeleteAsync(id)).ReturnsAsync(true);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.DeleteAsync(id)).ReturnsAsync(false);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Test_ReturnsOk_WhenConnectionExists()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(MakeConnection(id));
        _registryMock.Setup(r => r.TestConnectionAsync(id)).ReturnsAsync(true);

        var result = await _controller.Test(id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Test_ReturnsNotFound_WhenConnectionDoesNotExist()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((Connection?)null);

        var result = await _controller.Test(id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Test_ReturnsOkWithFalse_WhenPingFails()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(MakeConnection(id));
        _registryMock.Setup(r => r.TestConnectionAsync(id)).ReturnsAsync(false);

        var result = await _controller.Test(id);

        result.Should().BeOfType<OkObjectResult>();
        var body = result.As<OkObjectResult>().Value;
        body.Should().NotBeNull();
    }
}
