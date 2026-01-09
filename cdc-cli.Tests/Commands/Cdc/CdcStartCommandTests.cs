using System.Net;
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
/// Unit tests for CdcStartCommand
/// </summary>
public class CdcStartCommandTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<CdcApiClient>> _mockApiLogger;
    private readonly Mock<ILogger<CdcStartCommand>> _mockLogger;
    private readonly Mock<ILogger<JsonHandler>> _mockJsonLogger;
    private readonly CliConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly CdcApiClient _apiClient;
    private readonly JsonHandler _jsonHandler;

    /// <summary>
    /// Initializes a new instance of the CdcStartCommandTests class
    /// </summary>
    public CdcStartCommandTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockApiLogger = new Mock<ILogger<CdcApiClient>>();
        _mockLogger = new Mock<ILogger<CdcStartCommand>>();
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
    /// Tests that command can be created with valid dependencies
    /// </summary>
    [Fact]
    public void Constructor_WithValidDependencies_CreatesCommand()
    {
        // Act
        var command = new CdcStartCommand(_apiClient, _jsonHandler, _mockLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("start");
        command.Description.Should().Contain("Start CDC");
    }

    /// <summary>
    /// Tests that constructor validates apiClient parameter
    /// </summary>
    [Fact]
    public void Constructor_WithNullApiClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CdcStartCommand(null!, _jsonHandler, _mockLogger.Object, _configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("apiClient");
    }

    /// <summary>
    /// Tests that constructor validates jsonHandler parameter
    /// </summary>
    [Fact]
    public void Constructor_WithNullJsonHandler_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CdcStartCommand(_apiClient, null!, _mockLogger.Object, _configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonHandler");
    }

    /// <summary>
    /// Tests that constructor validates logger parameter
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CdcStartCommand(_apiClient, _jsonHandler, null!, _configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    /// <summary>
    /// Tests that constructor validates configuration parameter
    /// </summary>
    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CdcStartCommand(_apiClient, _jsonHandler, _mockLogger.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    /// <summary>
    /// Tests that command has expected options
    /// </summary>
    [Fact]
    public void Command_HasExpectedOptions()
    {
        // Arrange
        var command = new CdcStartCommand(_apiClient, _jsonHandler, _mockLogger.Object, _configuration);

        // Act
        var options = command.Options.ToList();

        // Assert
        options.Should().NotBeEmpty();
        options.Should().Contain(o => o.Name == "session" || o.Aliases.Contains("--session"));
        options.Should().Contain(o => o.Name == "include" || o.Aliases.Contains("--include"));
        options.Should().Contain(o => o.Name == "exclude" || o.Aliases.Contains("--exclude"));
        options.Should().Contain(o => o.Name == "data" || o.Aliases.Contains("--data"));
        options.Should().Contain(o => o.Name == "file" || o.Aliases.Contains("--file"));
    }

    /// <summary>
    /// Tests that API client can execute start request successfully
    /// </summary>
    [Fact]
    public async Task ApiClient_ExecuteStartRequest_ReturnsSuccessResponse()
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

        SetupHttpResponse(expectedResponse);

        // Act
        var response = await _apiClient.PostAsync<StartCdcRequest, StartCdcResponse>("/api/cdc/start", request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.SessionName.Should().Be("test-session");
        response.TablesEnabled.Should().Contain("dbo.Orders");
    }

    /// <summary>
    /// Tests that API client handles error responses
    /// </summary>
    [Fact]
    public async Task ApiClient_ExecuteStartRequestWithError_ThrowsException()
    {
        // Arrange
        var request = new StartCdcRequest
        {
            SessionName = "test-session",
            TablesToInclude = new List<string> { "dbo.Orders" }
        };

        SetupHttpErrorResponse(HttpStatusCode.InternalServerError);

        // Act & Assert
        var act = async () => await _apiClient.PostAsync<StartCdcRequest, StartCdcResponse>("/api/cdc/start", request);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>
    /// Sets up a successful HTTP response mock
    /// </summary>
    /// <param name="response">Response object</param>
    private void SetupHttpResponse(StartCdcResponse response)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response, options);
        
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// Sets up an HTTP error response mock
    /// </summary>
    /// <param name="statusCode">HTTP status code</param>
    private void SetupHttpErrorResponse(HttpStatusCode statusCode)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent("Error")
            });
    }
}
