using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net;
using Xunit;
using FluentAssertions;
using Moq;
using Softbase.Cdc.Trace;
using Softbase.Cdc.Data;
using Softbase.Cdc.Models;
using cdc_api.Controllers;

namespace cdc_api.Tests.Controllers;

public class SnapshotControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SnapshotControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real services with mocks for testing
                var mockSnapshotManager = new Mock<ISnapshotManager>();
                // Setup mock returns for successful operations
                mockSnapshotManager.Setup(x => x.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot created successfully" });

                mockSnapshotManager.Setup(x => x.RestoreSnapshotAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot restored successfully" });

                mockSnapshotManager.Setup(x => x.DropSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot deleted successfully" });

                mockSnapshotManager.Setup(x => x.ListSnapshotsAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new List<SnapshotInfo>
                    {
                        new SnapshotInfo
                        {
                            SnapshotName = "TestSnapshot",
                            SourceDatabase = "TestDB",
                            CreatedTime = DateTime.UtcNow
                        }
                    });

                services.AddSingleton(mockSnapshotManager.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateSnapshot_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateSnapshotRequest
        {
            DatabaseName = "TestDB",
            SnapshotName = "TestSnapshot",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/snapshot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateSnapshot_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateSnapshotRequest
        {
            DatabaseName = "",
            SnapshotName = "",
            ConnectionString = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/snapshot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RestoreSnapshot_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new RestoreSnapshotRequest
        {
            DatabaseName = "TestDB",
            SnapshotName = "TestSnapshot",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/snapshot/restore", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListSnapshots_ValidRequest_ReturnsOk()
    {
        // Arrange
        var databaseName = "TestDB";
        var connectionString = "Server=test;Database=test;Trusted_Connection=true;";

        // Act
        var response = await _client.GetAsync($"/api/snapshot/{databaseName}/snapshots?connectionString={connectionString}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSnapshotInfo_ValidRequest_ReturnsOk()
    {
        // Arrange
        var databaseName = "TestDB";
        var snapshotName = "TestSnapshot";
        var connectionString = "Server=test;Database=test;Trusted_Connection=true;";

        // Act
        var response = await _client.GetAsync($"/api/snapshot/{databaseName}/snapshots/{snapshotName}?connectionString={connectionString}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteSnapshot_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new DeleteSnapshotRequest
        {
            SnapshotName = "TestSnapshot",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;"
        };

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/snapshot")
        {
            Content = JsonContent.Create(request)
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}