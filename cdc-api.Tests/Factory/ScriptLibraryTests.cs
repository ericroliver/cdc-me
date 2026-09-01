using System;
using System.Collections.Generic;
using cdc_api.Controllers.Factory;
using CdcModels.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories;
using Xunit;

namespace cdc_api.Tests.Factory;

public class ScriptLibraryTests
{
    [Fact]
    public void MapScript_MapsAllFieldsCorrectly()
    {
        var values = new object?[]
        {
            Guid.NewGuid(), "create-branches", "Create N branches", "SqlScript",
            "SELECT 1", null, Guid.NewGuid(), 1, DateTime.UtcNow, DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "description", "type", "content", "file_path", "script_group_id", "order", "created_at", "updated_at" };
        var reader = new FakeDataReader(values, schema);

        var script = ScriptLibrary.MapScript(reader);

        script.Id.Should().Be((Guid)values[0]!);
        script.Name.Should().Be("create-branches");
        script.Description.Should().Be("Create N branches");
        script.Type.Should().Be("SqlScript");
        script.Content.Should().Be("SELECT 1");
        script.FilePath.Should().BeNull();
    }
}

public class ScriptsControllerTests
{
    private readonly Mock<IScriptLibrary> _libraryMock = new();
    private readonly ILogger<ScriptsController> _logger = NullLogger<ScriptsController>.Instance;
    private readonly ScriptsController _controller;

    public ScriptsControllerTests()
    {
        _controller = new ScriptsController(_libraryMock.Object, _logger);
    }

    private static Script MakeScript(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "test-script",
        Type = "SqlScript",
        Content = "SELECT 1",
        ScriptGroupId = Guid.NewGuid(),
        Order = 1
    };

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenScriptGroupIdIsEmpty()
    {
        var dto = new CreateScriptDto
        {
            Name = "test",
            Content = "SELECT 1",
            ScriptGroupId = Guid.Empty
        };

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _libraryMock.Verify(l => l.CreateScriptAsync(It.IsAny<CreateScriptRequest>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNoContentOrFilePath()
    {
        var dto = new CreateScriptDto { Name = "test", ScriptGroupId = Guid.NewGuid() };
        var result = await _controller.Create(dto);
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenBothContentAndFilePathProvided()
    {
        var dto = new CreateScriptDto
        {
            Name = "test",
            Content = "SELECT 1",
            FilePath = "/tmp/test.sql",
            ScriptGroupId = Guid.NewGuid()
        };

        var result = await _controller.Create(dto);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _libraryMock.Verify(l => l.CreateScriptAsync(It.IsAny<CreateScriptRequest>()), Times.Never);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAt_WhenOnlyFilePathProvided()
    {
        var dto = new CreateScriptDto
        {
            Name = "test",
            FilePath = "/tmp/test.sql",
            ScriptGroupId = Guid.NewGuid()
        };
        _libraryMock.Setup(l => l.CreateScriptAsync(It.IsAny<CreateScriptRequest>()))
                    .ReturnsAsync(MakeScript());

        var result = await _controller.Create(dto);
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_ReturnsCreatedAt_WhenValid()
    {
        var dto = new CreateScriptDto
        {
            Name = "test",
            Content = "SELECT 1",
            ScriptGroupId = Guid.NewGuid()
        };
        _libraryMock.Setup(l => l.CreateScriptAsync(It.IsAny<CreateScriptRequest>()))
                    .ReturnsAsync(MakeScript());

        var result = await _controller.Create(dto);
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _libraryMock.Setup(l => l.GetScriptAsync(id)).ReturnsAsync((Script?)null);

        var result = await _controller.GetById(id);
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ReturnsOk_WhenFound()
    {
        var id = Guid.NewGuid();
        _libraryMock.Setup(l => l.GetScriptAsync(id)).ReturnsAsync(MakeScript(id));

        var result = await _controller.GetById(id);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task List_ReturnsAllScripts()
    {
        _libraryMock.Setup(l => l.ListScriptsAsync(null))
                    .ReturnsAsync(new List<Script> { MakeScript(), MakeScript() });

        var result = await _controller.List(null);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _libraryMock.Setup(l => l.DeleteScriptAsync(id)).ReturnsAsync(false);

        var result = await _controller.Delete(id);
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        var id = Guid.NewGuid();
        _libraryMock.Setup(l => l.DeleteScriptAsync(id)).ReturnsAsync(true);

        var result = await _controller.Delete(id);
        result.Should().BeOfType<NoContentResult>();
    }
}
