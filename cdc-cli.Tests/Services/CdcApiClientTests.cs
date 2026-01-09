using System.Net;
using cdc_cli.Configuration;
using cdc_cli.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace cdc_cli.Tests.Services;

/// <summary>
/// Tests for CDC API client
/// </summary>
public class CdcApiClientTests
{
    private readonly Mock<ILogger<CdcApiClient>> _loggerMock;
    private readonly CliConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the CdcApiClientTests class
    /// </summary>
    public CdcApiClientTests()
    {
        _loggerMock = new Mock<ILogger<CdcApiClient>>();
        _configuration = new CliConfiguration { BaseUrl = "http://localhost:5000" };
    }

    /// <summary>
    /// Tests that constructor validates parameters
    /// </summary>
    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CdcApiClient(null!, _loggerMock.Object, _configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    /// <summary>
    /// Tests that constructor validates logger parameter
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new CdcApiClient(httpClient, null!, _configuration);

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
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var act = () => new CdcApiClient(httpClient, _loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    /// <summary>
    /// Tests that PostAsync validates endpoint parameter
    /// </summary>
    [Fact]
    public async Task PostAsync_WithEmptyEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new CdcApiClient(httpClient, _loggerMock.Object, _configuration);

        // Act
        var act = async () => await client.PostAsync<object, object>("", new object());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    /// <summary>
    /// Tests that GetAsync validates endpoint parameter
    /// </summary>
    [Fact]
    public async Task GetAsync_WithEmptyEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new CdcApiClient(httpClient, _loggerMock.Object, _configuration);

        // Act
        var act = async () => await client.GetAsync<object>("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    /// <summary>
    /// Tests that DeleteAsync validates endpoint parameter
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithEmptyEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var httpClient = new HttpClient();
        var client = new CdcApiClient(httpClient, _loggerMock.Object, _configuration);

        // Act
        var act = async () => await client.DeleteAsync<object>("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be empty*");
    }

    /// <summary>
    /// Tests successful POST request with mock handler
    /// </summary>
    [Fact]
    public async Task PostAsync_WithSuccessfulResponse_ReturnsDeserializedObject()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseContent = """{"success":true,"message":"Test response"}""";

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var client = new CdcApiClient(httpClient, _loggerMock.Object, _configuration);

        // Act
        var result = await client.PostAsync<TestRequest, TestResponse>(
            "api/test",
            new TestRequest { Name = "test" });

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Test response");
    }

    /// <summary>
    /// Test request model
    /// </summary>
    private class TestRequest
    {
        /// <summary>
        /// Test name property
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test response model
    /// </summary>
    private class TestResponse
    {
        /// <summary>
        /// Test success property
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Test message property
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
