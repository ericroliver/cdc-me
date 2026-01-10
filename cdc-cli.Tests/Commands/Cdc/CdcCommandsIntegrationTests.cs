using System.Net;
using System.Text;
using cdc_cli.Commands.Cdc;
using cdc_cli.Configuration;
using cdc_cli.Services;
using CdcModels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace cdc_cli.Tests.Commands.Cdc;

/// <summary>
/// Integration tests for CDC commands (start, stop, capture)
/// </summary>
public class CdcCommandsIntegrationTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<CdcApiClient>> _mockApiLogger;
    private readonly Mock<ILogger<CdcStartCommand>> _mockStartLogger;
    private readonly Mock<ILogger<CdcStopCommand>> _mockStopLogger;
    private readonly Mock<ILogger<CdcCaptureCommand>> _mockCaptureLogger;
    private readonly Mock<ILogger<JsonHandler>> _mockJsonLogger;
    private readonly CliConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly CdcApiClient _apiClient;
    private readonly JsonHandler _jsonHandler;

    /// <summary>
    /// Initializes a new instance of the CdcCommandsIntegrationTests class
    /// </summary>
    public CdcCommandsIntegrationTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockApiLogger = new Mock<ILogger<CdcApiClient>>();
        _mockStartLogger = new Mock<ILogger<CdcStartCommand>>();
        _mockStopLogger = new Mock<ILogger<CdcStopCommand>>();
        _mockCaptureLogger = new Mock<ILogger<CdcCaptureCommand>>();
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
    /// Tests that CDC start command can be created
    /// </summary>
    [Fact]
    public void CdcStartCommand_CanBeCreated()
    {
        // Act
        var command = new CdcStartCommand(_apiClient, _jsonHandler, _mockStartLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("start");
        command.Description.Should().Contain("Start CDC");
    }

    /// <summary>
    /// Tests that CDC stop command can be created
    /// </summary>
    [Fact]
    public void CdcStopCommand_CanBeCreated()
    {
        // Act
        var command = new CdcStopCommand(_apiClient, _jsonHandler, _mockStopLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("stop");
        command.Description.Should().Contain("Stop CDC");
    }

    /// <summary>
    /// Tests that CDC capture command can be created
    /// </summary>
    [Fact]
    public void CdcCaptureCommand_CanBeCreated()
    {
        // Act
        var command = new CdcCaptureCommand(_apiClient, _jsonHandler, _mockCaptureLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("capture");
        command.Description.Should().Contain("Capture CDC");
    }

    /// <summary>
    /// Tests API client can make start request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeStartRequest_Success()
    {
        // Arrange
        var request = new StartCdcRequest
        {
            SessionName = "test-session",
            TablesToInclude = new List<string> { "dbo.Orders" }
        };

        var expectedResponse = new StartCdcResponse
        {
            Success = true,
            SessionName = "test-session",
            TablesEnabled = new List<string> { "dbo.Orders" },
            Message = "CDC started"
        };

        SetupHttpResponse("/api/cdc/start", expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<StartCdcRequest, StartCdcResponse>("/api/cdc/start", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.SessionName.Should().Be("test-session");
        response.TablesEnabled.Should().Contain("dbo.Orders");
    }

    /// <summary>
    /// Tests API client can make stop request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeStopRequest_Success()
    {
        // Arrange
        var request = new StopCdcRequest
        {
            SessionName = "test-session",
            CaptureName = "baseline",
            CaptureType = "Baseline"
        };

        var expectedResponse = new StopCdcResponse
        {
            Success = true,
            SessionName = "test-session",
            CaptureName = "baseline",
            TotalRecords = 100,
            TablesWithChanges = new List<string> { "dbo.Orders" }
        };

        SetupHttpResponse("/api/cdc/stop", expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<StopCdcRequest, StopCdcResponse>("/api/cdc/stop", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.SessionName.Should().Be("test-session");
        response.CaptureName.Should().Be("baseline");
        response.TotalRecords.Should().Be(100);
    }

    /// <summary>
    /// Tests API client can make capture request
    /// </summary>
    [Fact]
    public async Task ApiClient_CanMakeCaptureRequest_Success()
    {
        // Arrange
        var request = new CaptureCdcRequest
        {
            SessionName = "test-session",
            CaptureName = "checkpoint-1",
            CaptureType = "Intermediate"
        };

        var expectedResponse = new CaptureCdcResponse
        {
            Success = true,
            SessionName = "test-session",
            CaptureName = "checkpoint-1",
            CaptureType = "Intermediate",
            TotalRecords = 50,
            TablesWithChanges = new List<string> { "dbo.Orders" }
        };

        SetupHttpResponse("/api/cdc/capture", expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<CaptureCdcRequest, CaptureCdcResponse>("/api/cdc/capture", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.SessionName.Should().Be("test-session");
        response.CaptureName.Should().Be("checkpoint-1");
        response.TotalRecords.Should().Be(50);
    }

    /// <summary>
    /// Tests API error handling for start request
    /// </summary>
    [Fact]
    public async Task ApiClient_StartRequestWithApiError_ThrowsException()
    {
        // Arrange
        var request = new StartCdcRequest
        {
            SessionName = "test-session",
            TablesToInclude = new List<string> { "dbo.Orders" }
        };

        SetupHttpErrorResponse("/api/cdc/start", HttpStatusCode.InternalServerError, "Database error");

        // Act & Assert
        var act = async () => await _apiClient.PostAsync<StartCdcRequest, StartCdcResponse>("/api/cdc/start", request);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Tests API error handling for stop request
    /// </summary>
    [Fact]
    public async Task ApiClient_StopRequestWithApiError_ThrowsException()
    {
        // Arrange
        var request = new StopCdcRequest
        {
            SessionName = "test-session",
            CaptureName = "baseline"
        };

        SetupHttpErrorResponse("/api/cdc/stop", HttpStatusCode.BadRequest, "Invalid request");

        // Act & Assert
        var act = async () => await _apiClient.PostAsync<StopCdcRequest, StopCdcResponse>("/api/cdc/stop", request);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Tests complete workflow simulation with API calls
    /// </summary>
    [Fact]
    public async Task CompleteWorkflow_StartCaptureStop_AllSucceed()
    {
        // Arrange
        var sessionName = "workflow-test";

        var startRequest = new StartCdcRequest
        {
            SessionName = sessionName,
            TablesToInclude = new List<string> { "dbo.Orders", "dbo.Customers" }
        };

        var captureRequest = new CaptureCdcRequest
        {
            SessionName = sessionName,
            CaptureName = "checkpoint-1",
            CaptureType = "Intermediate"
        };

        var stopRequest = new StopCdcRequest
        {
            SessionName = sessionName,
            CaptureName = "final",
            CaptureType = "Baseline"
        };

        SetupHttpResponse("/api/cdc/start", new StartCdcResponse { Success = true, SessionName = sessionName });
        SetupHttpResponse("/api/cdc/capture", new CaptureCdcResponse { Success = true, SessionName = sessionName, CaptureName = "checkpoint-1" });
        SetupHttpResponse("/api/cdc/stop", new StopCdcResponse { Success = true, SessionName = sessionName, CaptureName = "final" });

        // Act & Assert - Start
        var startResponse = await _apiClient.PostAsync<StartCdcRequest, StartCdcResponse>("/api/cdc/start", startRequest);
        startResponse.Success.Should().BeTrue();

        // Act & Assert - Capture
        var captureResponse = await _apiClient.PostAsync<CaptureCdcRequest, CaptureCdcResponse>("/api/cdc/capture", captureRequest);
        captureResponse.Success.Should().BeTrue();

        // Act & Assert - Stop
        var stopResponse = await _apiClient.PostAsync<StopCdcRequest, StopCdcResponse>("/api/cdc/stop", stopRequest);
        stopResponse.Success.Should().BeTrue();
    }

    /// <summary>
    /// Sets up a successful HTTP response mock
    /// </summary>
    /// <typeparam name="T">Response type</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="response">Response object</param>
    private void SetupHttpResponse<T>(string endpoint, T response)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response, options);

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
