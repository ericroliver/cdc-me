using System;
using System.Collections.Generic;
using CdcModels.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories;
using cdc_api.Controllers.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

public class ScriptGroupRepositoryTests
{
    private readonly ILogger<ScriptGroupRepository> _logger = NullLogger<ScriptGroupRepository>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new ScriptGroupRepository(null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new ScriptGroupRepository("Host=localhost", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void MapGroup_MapsAllFieldsCorrectly()
    {
        var values = new object?[]
        {
            Guid.NewGuid(), "10branches", "Create branches", 1, 2, DateTime.UtcNow, DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "description", "layer", "order", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var group = ScriptGroupRepository.MapGroup(reader);

        group.Id.Should().Be((Guid)values[0]!);
        group.Name.Should().Be("10branches");
        group.Description.Should().Be("Create branches");
        group.Layer.Should().Be(1);
        group.Order.Should().Be(2);
    }

    [Fact]
    public void MapGroup_HandlesNullDescription()
    {
        var values = new object?[]
        {
            Guid.NewGuid(), "test", DBNull.Value, 0, 0, DateTime.UtcNow, DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "description", "layer", "order", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var group = ScriptGroupRepository.MapGroup(reader);

        group.Description.Should().BeNull();
    }

    [Fact]
    public async Task CreateGroupAsync_ThrowsWhenNameEmpty()
    {
        var repo = new ScriptGroupRepository("Host=localhost", _logger);
        var request = new CreateScriptGroupRequest { Name = "" };

        var act = () => repo.CreateGroupAsync(request);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Name is required*");
    }
}

public class ScriptGroupsControllerTests
{
    private readonly Mock<IScriptGroupRepository> _repoMock = new();
    private readonly Mock<IScriptLibrary> _scriptLibraryMock = new();
    private readonly ILogger<ScriptGroupsController> _logger = NullLogger<ScriptGroupsController>.Instance;
    private readonly ScriptGroupsController _controller;

    public ScriptGroupsControllerTests()
    {
        _controller = new ScriptGroupsController(_repoMock.Object, _scriptLibraryMock.Object, _logger);
    }

    private static ScriptGroup MakeGroup(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "10branches",
        Description = "Create branches",
        Layer = 1,
        Order = 2,
        Dependencies = new List<Guid>()
    };

    [Fact]
    public async Task Create_ReturnsCreatedAt_WhenValid()
    {
        var dto = new CreateScriptGroupDto { Name = "test", Layer = 0, Order = 1 };
        _repoMock.Setup(r => r.CreateGroupAsync(It.IsAny<CreateScriptGroupRequest>()))
                 .ReturnsAsync(MakeGroup());

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task List_ReturnsAllGroups()
    {
        _repoMock.Setup(r => r.ListGroupsAsync(null)).ReturnsAsync(new List<ScriptGroup>
        {
            MakeGroup(),
            new ScriptGroup
            {
                Id = Guid.NewGuid(),
                Name = "bill-split-merge",
                Layer = 1,
                Order = 3
            }
        });

        var result = await _controller.List(null);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetGroupAsync(id)).ReturnsAsync((ScriptGroup?)null);

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.GetGroupAsync(id)).ReturnsAsync(MakeGroup(id));
        _scriptLibraryMock.Setup(s => s.ListScriptsAsync(id)).ReturnsAsync(new List<Script>());

        var result = await _controller.GetById(id);

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.UpdateGroupAsync(id, It.IsAny<UpdateScriptGroupRequest>()))
                 .ReturnsAsync((ScriptGroup?)null);

        var result = await _controller.Update(id, new UpdateScriptGroupDto());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.DeleteGroupAsync(id)).ReturnsAsync(false);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.DeleteGroupAsync(id)).ReturnsAsync(true);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NoContentResult>();
    }
}
