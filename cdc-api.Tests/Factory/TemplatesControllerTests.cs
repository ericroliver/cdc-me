using CdcModels.Factory;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Engine;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using cdc_api.Controllers.Factory;
using Xunit;

namespace cdc_api.Tests.Factory;

public class TemplatesControllerTests
{
    private readonly Mock<IDatabaseTemplateRepository> _templateRepoMock = new();
    private readonly Mock<ITemplateStorageProvider> _storageProviderMock = new();
    private readonly TemplatesController _controller;

    public TemplatesControllerTests()
    {
        _storageProviderMock.Setup(s => s.Exists(It.IsAny<string>())).Returns(true);
        _controller = new TemplatesController(
            _templateRepoMock.Object,
            _storageProviderMock.Object,
            NullLogger<TemplatesController>.Instance);
    }

    private static Template MakeTemplate(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test Template",
        Version = "1.0",
        Platform = "SqlServer",
        FilePath = "/backups/test.bak",
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenDeleted()
    {
        var template = MakeTemplate();
        _templateRepoMock.Setup(r => r.GetByIdAsync(template.Id)).ReturnsAsync(template);
        _templateRepoMock.Setup(r => r.DeleteAsync(template.Id)).ReturnsAsync(true);

        var result = await _controller.Delete(template.Id);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenNotFound()
    {
        var id = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.DeleteAsync(id)).ReturnsAsync(false);

        var result = await _controller.Delete(id);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Delete_ReturnsConflict_WhenReferencedByOrders()
    {
        var id = Guid.NewGuid();
        _templateRepoMock.Setup(r => r.DeleteAsync(id))
            .ThrowsAsync(new ReferencedByOrdersException(
                "template",
                "Cannot delete template referenced by existing orders."));

        var result = await _controller.Delete(id);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
    }
}
