using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Net;
using Xunit;
using FluentAssertions;
using Moq;
using Softbase.Cdc.Trace;
using Softbase.Cdc.Models;
using cdc_api.Controllers;

namespace cdc_api.Tests.Controllers;

public class TraceControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TraceControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real services with mocks for testing
                var mockTraceManager = new Mock<ITraceManager>();
                var mockTraceDataProvider = new Mock<ITraceDataProvider>();

                // Setup mock returns for successful operations
                var testSession = new TraceSession
                {
                    SessionId = Guid.NewGuid(),
                    SessionName = "TestSession",
                    DatabaseName = "TestDB",
                    Status = TraceStatus.Running,
                    StartTime = DateTime.UtcNow,
                    Configuration = new TraceConfiguration()
                };

                mockTraceManager.Setup(x => x.StartTraceAsync(It.IsAny<TraceConfiguration>(), It.IsAny<string>()))
                    .ReturnsAsync(testSession);

                mockTraceManager.Setup(x => x.StopTraceAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(testSession);

                mockTraceManager.Setup(x => x.IsTraceRunningAsync(It.IsAny<string>()))
                    .ReturnsAsync(true);

                mockTraceManager.Setup(x => x.ExportTraceDataAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                    .ReturnsAsync("/path/to/export");

                mockTraceDataProvider.Setup(x => x.GetTraceSessionByNameAsync(It.IsAny<string>()))
                    .ReturnsAsync(testSession);

                mockTraceDataProvider.Setup(x => x.GetTraceSessionAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(testSession);

                mockTraceDataProvider.Setup(x => x.GetTraceSessionsAsync())
                    .ReturnsAsync(new List<TraceSession> { testSession });

                mockTraceDataProvider.Setup(x => x.GetTraceEventCountAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(100);

                mockTraceDataProvider.Setup(x => x.GetTraceEventsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
                    .ReturnsAsync(new List<TraceEvent>());

                services.AddSingleton(mockTraceManager.Object);
                services.AddSingleton(mockTraceDataProvider.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task StartTrace_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new StartTraceRequest
        {
            SessionName = "TestSession",
            DatabaseName = "TestDB",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;",
            MaxFileSize = 100,
            MaxFiles = 5
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/trace/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StartTrace_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new StartTraceRequest
        {
            SessionName = "",
            DatabaseName = "",
            ConnectionString = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/trace/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StopTrace_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new StopTraceRequest
        {
            SessionName = "TestSession"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/trace/stop", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTraceStatus_ValidRequest_ReturnsOk()
    {
        // Arrange
        var sessionName = "TestSession";

        // Act
        var response = await _client.GetAsync($"/api/trace/status/{sessionName}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListTraceSessions_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/trace/sessions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExportTrace_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ExportTraceRequest
        {
            SessionName = "TestSession",
            TraceConnectionString = "Server=test;Database=trace;Trusted_Connection=true;"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/trace/export", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTraceEvents_ValidRequest_ReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var limit = 100;
        var offset = 0;

        // Act
        var response = await _client.GetAsync($"/api/trace/sessions/{sessionId}/events?limit={limit}&offset={offset}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteTraceSession_ValidRequest_ReturnsOk()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/trace/sessions/{sessionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}