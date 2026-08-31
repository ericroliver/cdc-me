using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using cdc_api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softbase.Cdc.Data;
using Softbase.Cdc.Models;
using Softbase.Cdc.Trace;
using Xunit;

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
                mockSnapshotManager.Setup(x => x.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot created successfully" });

                mockSnapshotManager.Setup(x => x.RestoreSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot restored successfully" });

                mockSnapshotManager.Setup(x => x.DropSnapshotAsync(It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot deleted successfully" });

                mockSnapshotManager.Setup(x => x.ListSnapshotsAsync(It.IsAny<string>()))
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
            SnapshotName = "TestSnapshot"
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
            SnapshotName = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/snapshot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSnapshot_FailedResult_ReturnsBadRequest()
    {
        // Arrange — use a separate factory that returns failure for a specific DB name
        var failClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockSnapshotManager = new Mock<ISnapshotManager>();
                mockSnapshotManager.Setup(x => x.CreateSnapshotAsync("FailDB", It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = false, Message = "Cannot open database \"CdcTestDB\" requested by the login. The login failed." });
                mockSnapshotManager.Setup(x => x.RestoreSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot restored successfully" });
                mockSnapshotManager.Setup(x => x.DropSnapshotAsync(It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot deleted successfully" });
                mockSnapshotManager.Setup(x => x.ListSnapshotsAsync(It.IsAny<string>()))
                    .ReturnsAsync(new List<SnapshotInfo>());
                services.AddSingleton(mockSnapshotManager.Object);
            });
        }).CreateClient();

        var request = new CreateSnapshotRequest
        {
            DatabaseName = "FailDB",
            SnapshotName = "TestSnapshot"
        };

        // Act
        var response = await failClient.PostAsJsonAsync("/api/snapshot", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("CdcTestDB");
        content.Should().NotContain("login failed");
    }

    [Fact]
    public async Task RestoreSnapshot_FailedResult_ReturnsBadRequest()
    {
        // Arrange — use a separate factory that returns failure for restore
        var failClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockSnapshotManager = new Mock<ISnapshotManager>();
                mockSnapshotManager.Setup(x => x.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot created successfully" });
                mockSnapshotManager.Setup(x => x.RestoreSnapshotAsync("NonexistentSnapshot", It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = false, Message = "Snapshot 'NonexistentSnapshot' not found" });
                mockSnapshotManager.Setup(x => x.DropSnapshotAsync(It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot deleted successfully" });
                mockSnapshotManager.Setup(x => x.ListSnapshotsAsync(It.IsAny<string>()))
                    .ReturnsAsync(new List<SnapshotInfo>());
                services.AddSingleton(mockSnapshotManager.Object);
            });
        }).CreateClient();

        var request = new RestoreSnapshotRequest
        {
            DatabaseName = "TestDB",
            SnapshotName = "NonexistentSnapshot"
        };

        // Act
        var response = await failClient.PostAsJsonAsync("/api/snapshot/restore", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("not found");
    }

    [Fact]
    public async Task DeleteSnapshot_FailedResult_ReturnsNotFound()
    {
        // Arrange — use a separate factory that returns failure for deletion
        var failClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockSnapshotManager = new Mock<ISnapshotManager>();
                mockSnapshotManager.Setup(x => x.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot created successfully" });
                mockSnapshotManager.Setup(x => x.RestoreSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot restored successfully" });
                mockSnapshotManager.Setup(x => x.DropSnapshotAsync("NonexistentSnapshot"))
                    .ReturnsAsync(new SnapshotResult { Success = false, Message = "Snapshot not found" });
                mockSnapshotManager.Setup(x => x.ListSnapshotsAsync(It.IsAny<string>()))
                    .ReturnsAsync(new List<SnapshotInfo>());
                services.AddSingleton(mockSnapshotManager.Object);
            });
        }).CreateClient();

        // Act
        var response = await failClient.DeleteAsync("/api/snapshot/NonexistentSnapshot");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RestoreSnapshot_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new RestoreSnapshotRequest
        {
            DatabaseName = "TestDB",
            SnapshotName = "TestSnapshot"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/snapshot/restore", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestoreSnapshot_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new RestoreSnapshotRequest
        {
            DatabaseName = "",
            SnapshotName = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/snapshot/restore", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListSnapshots_ValidRequest_ReturnsOk()
    {
        // Arrange
        var databaseName = "TestDB";

        // Act
        var response = await _client.GetAsync($"/api/snapshot/{databaseName}/snapshots");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSnapshotInfo_ValidRequest_ReturnsOk()
    {
        // Arrange
        var databaseName = "TestDB";
        var snapshotName = "TestSnapshot";

        // Act
        var response = await _client.GetAsync($"/api/snapshot/{databaseName}/snapshots/{snapshotName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteSnapshot_ValidRequest_ReturnsOk()
    {
        // Arrange
        var snapshotName = "TestSnapshot";

        // Act
        // Using the new route-based DELETE endpoint
        var response = await _client.DeleteAsync($"/api/snapshot/{snapshotName}");

        // Assert
        // Expecting OK (200) for successful deletion with the new endpoint design
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
