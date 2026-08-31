using System.Net;
using System.Net.Http.Json;
using cdc_api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Softbase.Cdc.Models;
using Softbase.Cdc.Trace;
using Xunit;

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

                mockTraceManager.Setup(x => x.StartTraceAsync(It.IsAny<TraceConfiguration>()))
                    .ReturnsAsync(testSession);

                mockTraceManager.Setup(x => x.StopTraceAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(testSession);

                mockTraceManager.Setup(x => x.IsTraceRunningAsync(It.IsAny<string>()))
                    .ReturnsAsync(true);

                mockTraceManager.Setup(x => x.GetTraceStatusAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(new TraceStatus
                    {
                        SessionId = testSession.SessionId,
                        State = TraceStatus.Running,
                        StartedAt = testSession.StartTime,
                        EventCount = 100,
                        LastError = null
                    });

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
            DatabaseName = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/trace/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"error\"");
        content.Should().NotContain("\"success\"");
        content.Should().NotContain("\"sessionId\"");
    }

    [Fact]
    public async Task StartTrace_Exception_ReturnsCleanErrorEnvelope()
    {
        // Arrange — factory with mock that throws on StartTraceAsync
        var errorClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockTraceManager = new Mock<ITraceManager>();
                mockTraceManager.Setup(x => x.StartTraceAsync(It.IsAny<TraceConfiguration>()))
                    .ThrowsAsync(new InvalidOperationException("Database unreachable"));
                var mockTraceDataProvider = new Mock<ITraceDataProvider>();
                services.AddSingleton(mockTraceManager.Object);
                services.AddSingleton(mockTraceDataProvider.Object);
            });
        }).CreateClient();

        var request = new StartTraceRequest
        {
            SessionName = "ErrorSession",
            DatabaseName = "BadDB"
        };

        // Act
        var response = await errorClient.PostAsJsonAsync("/api/trace/start", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"error\"");
        content.Should().NotContain("\"success\"");
        content.Should().NotContain("\"message\"");
        content.Should().NotContain("\"sessionId\"");
        content.Should().NotContain("\"sessionName\"");
    }

    [Fact]
    public async Task GetTraceStatus_NonexistentSession_ReturnsNotFound()
    {
        // Arrange — use a factory that returns null for a specific session name
        var notFoundClient = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var mockTraceManager = new Mock<ITraceManager>();
                var mockTraceDataProvider = new Mock<ITraceDataProvider>();

                mockTraceDataProvider.Setup(x => x.GetTraceSessionByNameAsync("NonexistentSession"))
                    .ReturnsAsync((TraceSession?)null);

                services.AddSingleton(mockTraceManager.Object);
                services.AddSingleton(mockTraceDataProvider.Object);
            });
        }).CreateClient();

        // Act
        var response = await notFoundClient.GetAsync("/api/trace/status/NonexistentSession");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
            SessionName = "TestSession"
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
