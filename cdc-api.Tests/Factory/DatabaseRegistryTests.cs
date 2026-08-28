using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories;
using cdc_api.Controllers.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

public class DatabaseRegistryTests
{
    [Fact]
    public void MapProvisionedDatabase_MapsAllFields()
    {
        var values = new object?[]
        {
            Guid.NewGuid(), Guid.NewGuid(), "acme_db", Guid.NewGuid(), Guid.NewGuid(),
            "Active", DateTime.UtcNow, DBNull.Value
        };
        var schema = new[] { "id", "order_id", "database_name", "connection_id", "template_id", "status", "created_at", "decommissioned_at" };
        var reader = new FakeDataReader(values, schema);

        var db = DatabaseRegistry.MapProvisionedDatabase(reader);

        db.Id.Should().Be((Guid)values[0]!);
        db.OrderId.Should().Be((Guid)values[1]!);
        db.DatabaseName.Should().Be("acme_db");
        db.ConnectionId.Should().Be((Guid)values[3]!);
        db.TemplateId.Should().Be((Guid)values[4]!);
        db.Status.Should().Be("Active");
        db.DecommissionedAt.Should().BeNull();
    }
}

public class DatabasesControllerTests
{
    private readonly Mock<IDatabaseRegistry> _registryMock = new();
    private readonly DatabasesController _controller;

    public DatabasesControllerTests()
    {
        _controller = new DatabasesController(_registryMock.Object, NullLogger<DatabasesController>.Instance);
    }

    private static ProvisionedDatabase MakeDb(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        DatabaseName = "test_db",
        ConnectionId = Guid.NewGuid(),
        TemplateId = Guid.NewGuid(),
        Status = "Active",
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task List_ReturnsAllDatabases()
    {
        _registryMock.Setup(r => r.ListAsync()).ReturnsAsync(new List<ProvisionedDatabase>
        {
            MakeDb(), MakeDb()
        });

        var result = await _controller.List();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(MakeDb(id));

        var result = await _controller.GetById(id);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _registryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync((ProvisionedDatabase?)null);

        var result = await _controller.GetById(id);
        result.Result.Should().BeOfType<NotFoundResult>();
    }
}
