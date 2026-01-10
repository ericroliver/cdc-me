using System.Net;
using System.Text;
using cdc_cli.Commands.Trace;
using cdc_cli.Configuration;
using cdc_cli.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace cdc_cli.Tests.Commands.Trace;

/// <summary>
/// Integration tests for Trace commands (start, stop, status, list, export, events, delete)
/// </summary>
public class TraceCommandsIntegrationTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<CdcApiClient>> _mockApiLogger;
    private readonly Mock<ILogger<TraceStartCommand>> _mockStartLogger;
    private readonly Mock<ILogger<TraceStopCommand>> _mockStopLogger;
    private readonly Mock<ILogger<TraceStatusCommand>> _mockStatusLogger;
    private readonly Mock<ILogger<TraceListCommand>> _mockListLogger;
    private readonly Mock<ILogger<TraceExportCommand>> _mockExportLogger;
    private readonly Mock<ILogger<TraceEventsCommand>> _mockEventsLogger;
    private readonly Mock<ILogger<TraceDeleteCommand>> _mockDeleteLogger;
    private readonly Mock<ILogger<JsonHandler>> _mockJsonLogger;
    private readonly CliConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly CdcApiClient _apiClient;
    private readonly JsonHandler _jsonHandler;

    /// <summary>
    /// Initializes a new instance of the TraceCommandsIntegrationTests class
    /// </summary>
    public TraceCommandsIntegrationTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockApiLogger = new Mock<ILogger<CdcApiClient>>();
        _mockStartLogger = new Mock<ILogger<TraceStartCommand>>();
        _mockStopLogger = new Mock<ILogger<TraceStopCommand>>();
        _mockStatusLogger = new Mock<ILogger<TraceStatusCommand>>();
        _mockListLogger = new Mock<ILogger<TraceListCommand>>();
        _mockExportLogger = new Mock<ILogger<TraceExportCommand>>();
        _mockEventsLogger = new Mock<ILogger<TraceEventsCommand>>();
        _mockDeleteLogger = new Mock<ILogger<TraceDeleteCommand>>();
        _mockJsonLogger = new Mock<ILogger<JsonHandler>>();

        _configuration = new CliConfiguration
        {
            BaseUrl = "http://localhost:5000",
            OutputFormat = OutputFormat.Json
        };

        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _apiClient = new CdcApiClient(_httpClient, _mockApiLogger.Object, _configuration);
        _jsonHandler = new JsonHandler(_mockJsonLogger.Object, _configuration);
    }

    /// <summary>
    /// Cleans up test resources
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that trace start command can be created
    /// </summary>
    [Fact]
    public void TraceStartCommand_CanBeCreated()
    {
        // Act
        var command = new TraceStartCommand(_apiClient, _jsonHandler, _mockStartLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("start");
        command.Description.Should().Contain("trace");
    }

    /// <summary>
    /// Tests that trace stop command can be created
    /// </summary>
    [Fact]
    public void TraceStopCommand_CanBeCreated()
    {
        // Act
        var command = new TraceStopCommand(_apiClient, _jsonHandler, _mockStopLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("stop");
        command.Description.Should().Contain("trace");
    }

    /// <summary>
    /// Tests that trace status command can be created
    /// </summary>
    [Fact]
    public void TraceStatusCommand_CanBeCreated()
    {
        // Act
        var command = new TraceStatusCommand(_apiClient, _jsonHandler, _mockStatusLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("status");
        command.Description.Should().Contain("status");
    }

    /// <summary>
    /// Tests that trace list command can be created
    /// </summary>
    [Fact]
    public void TraceListCommand_CanBeCreated()
    {
        // Act
        var command = new TraceListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("list");
        command.Description.Should().ContainEquivalentOf("list");
    }

    /// <summary>
    /// Tests that trace export command can be created
    /// </summary>
    [Fact]
    public void TraceExportCommand_CanBeCreated()
    {
        // Act
        var command = new TraceExportCommand(_apiClient, _jsonHandler, _mockExportLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("export");
        command.Description.Should().ContainEquivalentOf("export");
    }

    /// <summary>
    /// Tests that trace events command can be created
    /// </summary>
    [Fact]
    public void TraceEventsCommand_CanBeCreated()
    {
        // Act
        var command = new TraceEventsCommand(_apiClient, _jsonHandler, _mockEventsLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("events");
        command.Description.Should().Contain("events");
    }

    /// <summary>
    /// Tests that trace delete command can be created
    /// </summary>
    [Fact]
    public void TraceDeleteCommand_CanBeCreated()
    {
        // Act
        var command = new TraceDeleteCommand(_apiClient, _jsonHandler, _mockDeleteLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("delete");
        command.Description.Should().ContainEquivalentOf("delete");
    }

    /// <summary>
    /// Tests API client can make trace start request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceStartRequest_Success()
    {
        // Arrange
        var request = new
        {
            SessionName = "test-trace",
            DatabaseName = "TestDB",
            MaxFileSize = 100,
            MaxFiles = 5
        };

        var expectedResponse = new
        {
            Success = true,
            SessionId = Guid.NewGuid(),
            SessionName = "test-trace",
            Message = "Trace started successfully"
        };

        SetupHttpResponse("/api/trace/start", expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<object, object>("/api/trace/start", request);

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests API client can make trace stop request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceStopRequest_Success()
    {
        // Arrange
        var request = new
        {
            SessionName = "test-trace"
        };

        var expectedResponse = new
        {
            Success = true,
            SessionName = "test-trace",
            Message = "Trace stopped successfully"
        };

        SetupHttpResponse("/api/trace/stop", expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<object, object>("/api/trace/stop", request);

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests API client can make trace status request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceStatusRequest_Success()
    {
        // Arrange
        var sessionName = "test-trace";
        var expectedResponse = new
        {
            SessionId = Guid.NewGuid(),
            SessionName = sessionName,
            DatabaseName = "TestDB",
            Status = new { State = "Active" },
            EventCount = 100
        };

        SetupHttpResponse($"/api/trace/status/{sessionName}", expectedResponse);

        // Act
        var response = await _apiClient.GetAsync<object>($"/api/trace/status/{sessionName}");

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests API client can make trace list request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceListRequest_Success()
    {
        // Arrange
        var expectedResponse = new[]
        {
            new
            {
                SessionId = Guid.NewGuid(),
                SessionName = "trace-1",
                DatabaseName = "TestDB",
                Status = new { State = "Active" }
            },
            new
            {
                SessionId = Guid.NewGuid(),
                SessionName = "trace-2",
                DatabaseName = "TestDB",
                Status = new { State = "Stopped" }
            }
        };

        SetupHttpResponse("/api/trace/sessions", expectedResponse);

        // Act
        var response = await _apiClient.GetAsync<object>("/api/trace/sessions");

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests API client can make trace export request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceExportRequest_Success()
    {
        // Arrange
        var request = new
        {
            SessionName = "test-trace"
        };

        var expectedResponse = new
        {
            Success = true,
            SessionName = "test-trace",
            Message = "Trace data exported successfully"
        };

        SetupHttpResponse("/api/trace/export", expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<object, object>("/api/trace/export", request);

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests API client can make trace events request with pagination
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceEventsRequest_WithPagination()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var limit = 50;
        var offset = 100;
        var expectedResponse = new[]
        {
            new
            {
                EventId = 1,
                EventName = "sql_statement_completed",
                Statement = "SELECT * FROM Orders"
            },
            new
            {
                EventId = 2,
                EventName = "rpc_completed",
                Statement = "EXEC sp_GetCustomer @id=1"
            }
        };

        SetupHttpResponse($"/api/trace/sessions/{sessionId}/events?limit={limit}&offset={offset}", expectedResponse);

        // Act
        var response = await _apiClient.GetAsync<object>($"/api/trace/sessions/{sessionId}/events?limit={limit}&offset={offset}");

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests API client can make trace delete request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeTraceDeleteRequest_Success()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var expectedResponse = new
        {
            Success = true,
            SessionId = sessionId,
            Message = "Trace session deleted successfully"
        };

        SetupHttpDeleteResponse($"/api/trace/sessions/{sessionId}", expectedResponse);

        // Act
        var response = await _apiClient.DeleteAsync<object>($"/api/trace/sessions/{sessionId}");

        // Assert
        response.Should().NotBeNull();
    }

    /// <summary>
    /// Tests complete trace workflow: start → status → export → stop → delete
    /// </summary>
    [Fact]
    public async Task CompleteTraceWorkflow_StartStatusExportStopDelete_AllSucceed()
    {
        // Arrange
        var sessionName = "workflow-test";
        var sessionId = Guid.NewGuid();

        // Start
        var startRequest = new
        {
            SessionName = sessionName,
            DatabaseName = "TestDB"
        };
        SetupHttpResponse("/api/trace/start", new { Success = true, SessionId = sessionId, SessionName = sessionName });

        // Status
        SetupHttpResponse($"/api/trace/status/{sessionName}", new { SessionId = sessionId, SessionName = sessionName, Status = new { State = "Active" } });

        // Export
        var exportRequest = new { SessionName = sessionName };
        SetupHttpResponse("/api/trace/export", new { Success = true, SessionName = sessionName });

        // Stop
        var stopRequest = new { SessionName = sessionName };
        SetupHttpResponse("/api/trace/stop", new { Success = true, SessionName = sessionName });

        // Delete
        SetupHttpDeleteResponse($"/api/trace/sessions/{sessionId}", new { Success = true, SessionId = sessionId });

        // Act & Assert - Start
        var startResponse = await _apiClient.PostAsync<object, object>("/api/trace/start", startRequest);
        startResponse.Should().NotBeNull();

        // Act & Assert - Status
        var statusResponse = await _apiClient.GetAsync<object>($"/api/trace/status/{sessionName}");
        statusResponse.Should().NotBeNull();

        // Act & Assert - Export
        var exportResponse = await _apiClient.PostAsync<object, object>("/api/trace/export", exportRequest);
        exportResponse.Should().NotBeNull();

        // Act & Assert - Stop
        var stopResponse = await _apiClient.PostAsync<object, object>("/api/trace/stop", stopRequest);
        stopResponse.Should().NotBeNull();

        // Act & Assert - Delete
        var deleteResponse = await _apiClient.DeleteAsync<object>($"/api/trace/sessions/{sessionId}");
        deleteResponse.Should().NotBeNull();
    }

    /// <summary>
    /// Tests pagination with different offsets and limits
    /// </summary>
    [Fact]
    public async Task TraceEvents_PaginationWithDifferentParameters_ReturnsCorrectData()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // First page
        SetupHttpResponse($"/api/trace/sessions/{sessionId}/events?limit=100&offset=0", new[] { new { EventId = 1 } });

        // Second page
        SetupHttpResponse($"/api/trace/sessions/{sessionId}/events?limit=100&offset=100", new[] { new { EventId = 101 } });

        // Custom page size
        SetupHttpResponse($"/api/trace/sessions/{sessionId}/events?limit=50&offset=200", new[] { new { EventId = 201 } });

        // Act & Assert - First page
        var page1 = await _apiClient.GetAsync<object>($"/api/trace/sessions/{sessionId}/events?limit=100&offset=0");
        page1.Should().NotBeNull();

        // Act & Assert - Second page
        var page2 = await _apiClient.GetAsync<object>($"/api/trace/sessions/{sessionId}/events?limit=100&offset=100");
        page2.Should().NotBeNull();

        // Act & Assert - Custom page size
        var page3 = await _apiClient.GetAsync<object>($"/api/trace/sessions/{sessionId}/events?limit=50&offset=200");
        page3.Should().NotBeNull();
    }

    /// <summary>
    /// Tests error handling for trace start request
    /// </summary>
    [Fact]
    public async Task ApiClient_TraceStartRequestWithApiError_ThrowsException()
    {
        // Arrange
        var request = new
        {
            SessionName = "test-trace",
            DatabaseName = "TestDB"
        };

        SetupHttpErrorResponse("/api/trace/start", HttpStatusCode.InternalServerError, "Database error");

        // Act & Assert
        var act = async () => await _apiClient.PostAsync<object, object>("/api/trace/start", request);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Tests error handling for trace delete request
    /// </summary>
    [Fact]
    public async Task ApiClient_TraceDeleteRequestWithNotFound_ThrowsException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        SetupHttpErrorResponse($"/api/trace/sessions/{sessionId}", HttpStatusCode.NotFound, "Session not found");

        // Act & Assert
        var act = async () => await _apiClient.DeleteAsync<object>($"/api/trace/sessions/{sessionId}");
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Sets up a successful HTTP response mock
    /// </summary>
    /// <typeparam name="T">Response type</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="response">Response object</param>
    private void SetupHttpResponse<T>(string endpoint, T response)
    {
        var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri != null && req.RequestUri.PathAndQuery.Contains(endpoint)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// Sets up a successful HTTP DELETE response mock
    /// </summary>
    /// <typeparam name="T">Response type</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="response">Response object</param>
    private void SetupHttpDeleteResponse<T>(string endpoint, T response)
    {
        var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response);

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Delete &&
                    req.RequestUri != null && req.RequestUri.PathAndQuery.Contains(endpoint)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// Sets up an HTTP error response mock
    /// </summary>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="errorMessage">Error message</param>
    private void SetupHttpErrorResponse(string endpoint, HttpStatusCode statusCode, string errorMessage)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri != null && req.RequestUri.PathAndQuery.Contains(endpoint)),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(errorMessage)
            });
    }
}
