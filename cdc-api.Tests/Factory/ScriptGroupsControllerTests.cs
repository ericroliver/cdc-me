using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Xunit;

namespace cdc_api.Tests.Factory;

/// <summary>
/// Integration tests for the ScriptGroups API endpoint.
/// Uses WebApplicationFactory to verify that the ApiBehaviorOptions
/// InvalidModelStateResponseFactory correctly sanitizes .NET type name
/// leaks from JSON deserialization errors.
/// </summary>
public class ScriptGroupsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ScriptGroupsApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Mock IScriptGroupRepository
                var mockScriptGroupRepo = new Mock<IScriptGroupRepository>();
                mockScriptGroupRepo.Setup(r => r.CreateGroupAsync(It.IsAny<CreateScriptGroupRequest>()))
                    .ReturnsAsync((CreateScriptGroupRequest req) => new ScriptGroup
                    {
                        Id = Guid.NewGuid(),
                        Name = req.Name,
                        Description = req.Description,
                        Layer = req.Layer,
                        Order = req.Order,
                        Dependencies = req.Dependencies,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                mockScriptGroupRepo.Setup(r => r.GetGroupAsync(It.IsAny<Guid>()))
                    .ReturnsAsync((ScriptGroup?)null);
                mockScriptGroupRepo.Setup(r => r.ListGroupsAsync(It.IsAny<int?>()))
                    .ReturnsAsync(new List<ScriptGroup>());

                services.AddSingleton(mockScriptGroupRepo.Object);

                // Mock IScriptLibrary
                var mockScriptLibrary = new Mock<IScriptLibrary>();
                mockScriptLibrary.Setup(s => s.ListScriptsAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(new List<Script>());
                services.AddSingleton(mockScriptLibrary.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateScriptGroup_WithInvalidGuidInDependencies_ReturnsCleanError()
    {
        // Arrange — send a body with a non-GUID string in the dependencies array
        var requestBody = new
        {
            name = "test-group",
            layer = 1,
            order = 1,
            dependencies = new[] { "not-a-guid" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/factory/script-groups", requestBody);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        content.Should().NotContain("System.Guid");
        content.Should().NotContain("BytePositionInLine");
        content.Should().NotContain("LineNumber");
        content.Should().NotContain("could not be converted");
    }

    [Fact]
    public async Task CreateScriptGroup_WithInvalidGuidInDependencies_ReturnsUserFriendlyMessage()
    {
        // Arrange
        var requestBody = new
        {
            name = "test-group",
            layer = 1,
            order = 1,
            dependencies = new[] { "also-not-a-guid" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/factory/script-groups", requestBody);
        var content = await response.Content.ReadAsStringAsync();

        // Assert — should contain a user-friendly message about invalid values
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        content.Should().Contain("invalid values");
    }

    [Fact]
    public async Task CreateScriptGroup_WithValidGuids_ReturnsCreated()
    {
        // Arrange — valid GUID dependencies should work
        var validDep = Guid.NewGuid();
        var requestBody = new
        {
            name = "test-group",
            layer = 1,
            order = 1,
            dependencies = new[] { validDep }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/factory/script-groups", requestBody);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
