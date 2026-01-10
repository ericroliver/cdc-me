using System.CommandLine;
using System.Net;
using System.Text;
using System.Text.Json;
using cdc_cli.Commands.Workflow;
using cdc_cli.Configuration;
using cdc_cli.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace cdc_cli.Tests.Commands.Workflow;

/// <summary>
/// Integration tests for Workflow commands (execute, status, list)
/// </summary>
public class WorkflowCommandsIntegrationTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly Mock<ILogger<CdcApiClient>> _mockApiLogger;
    private readonly Mock<ILogger<WorkflowExecuteCommand>> _mockExecuteLogger;
    private readonly Mock<ILogger<WorkflowStatusCommand>> _mockStatusLogger;
    private readonly Mock<ILogger<WorkflowListCommand>> _mockListLogger;
    private readonly Mock<ILogger<JsonHandler>> _mockJsonLogger;
    private readonly CliConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly CdcApiClient _apiClient;
    private readonly JsonHandler _jsonHandler;
    private readonly StringWriter _consoleOutput;
    private readonly TextWriter _originalOutput;
    private readonly StringWriter _consoleError;
    private readonly TextWriter _originalError;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Initializes a new instance of the WorkflowCommandsIntegrationTests class
    /// </summary>
    public WorkflowCommandsIntegrationTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _mockApiLogger = new Mock<ILogger<CdcApiClient>>();
        _mockExecuteLogger = new Mock<ILogger<WorkflowExecuteCommand>>();
        _mockStatusLogger = new Mock<ILogger<WorkflowStatusCommand>>();
        _mockListLogger = new Mock<ILogger<WorkflowListCommand>>();
        _mockJsonLogger = new Mock<ILogger<JsonHandler>>();

        _configuration = new CliConfiguration
        {
            BaseUrl = "http://localhost:5000",
            OutputFormat = OutputFormat.Json,
            Quiet = true // Suppress console messages in tests
        };

        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _apiClient = new CdcApiClient(_httpClient, _mockApiLogger.Object, _configuration);
        _jsonHandler = new JsonHandler(_mockJsonLogger.Object, _configuration);

        // Capture console output
        _consoleOutput = new StringWriter();
        _originalOutput = Console.Out;
        Console.SetOut(_consoleOutput);

        _consoleError = new StringWriter();
        _originalError = Console.Error;
        Console.SetError(_consoleError);
    }

    /// <summary>
    /// Cleans up test resources
    /// </summary>
    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        Console.SetError(_originalError);
        _consoleOutput.Dispose();
        _consoleError.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Tests that workflow execute command can be created
    /// </summary>
    [Fact]
    public void WorkflowExecuteCommand_CanBeCreated()
    {
        // Act
        var command = new WorkflowExecuteCommand(_apiClient, _jsonHandler, _mockExecuteLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("execute");
        command.Description.Should().Contain("workflow");
    }

    /// <summary>
    /// Tests that workflow status command can be created
    /// </summary>
    [Fact]
    public void WorkflowStatusCommand_CanBeCreated()
    {
        // Act
        var command = new WorkflowStatusCommand(_apiClient, _jsonHandler, _mockStatusLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("status");
        command.Description.Should().Contain("workflow");
    }

    /// <summary>
    /// Tests that workflow list command can be created
    /// </summary>
    [Fact]
    public void WorkflowListCommand_CanBeCreated()
    {
        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);

        // Assert
        command.Should().NotBeNull();
        command.Name.Should().Be("list");
        command.Description.Should().Contain("workflow");
    }

    /// <summary>
    /// Tests workflow execute with synchronous mode (default)
    /// </summary>
    [Fact]
    public async Task WorkflowExecute_SynchronousMode_ReturnsWorkflowResult()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var responseData = new
        {
            workflowId,
            workflowName = "TestWorkflow",
            startTime = DateTime.UtcNow,
            endTime = DateTime.UtcNow.AddMinutes(5),
            duration = TimeSpan.FromMinutes(5),
            success = true,
            steps = new[]
            {
                new
                {
                    stepName = "Create Baseline Snapshot",
                    startTime = DateTime.UtcNow,
                    endTime = DateTime.UtcNow.AddSeconds(30),
                    duration = TimeSpan.FromSeconds(30),
                    success = true,
                    message = "Snapshot created successfully"
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        // Create temp file with workflow config
        var tempFile = Path.GetTempFileName();
        try
        {
            var workflowConfig = new
            {
                workflowName = "TestWorkflow",
                databaseName = "TestDB",
                connectionString = "Server=localhost;Database=TestDB;",
                traceConnectionString = "Server=localhost;Database=CdcMe;",
                baselineSnapshotName = "baseline",
                testSnapshotName = "test",
                traceSessionName = "test-trace"
            };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(workflowConfig, JsonOptions));

            // Act
            var command = new WorkflowExecuteCommand(_apiClient, _jsonHandler, _mockExecuteLogger.Object, _configuration);
            var result = await InvokeCommandAsync(command, new[] { "--file", tempFile });

            // Assert
            result.Should().Be(0); // Success
            _consoleOutput.ToString().Should().Contain(workflowId.ToString());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Tests workflow execute with async mode
    /// </summary>
    [Fact]
    public async Task WorkflowExecute_AsyncMode_ReturnsWorkflowId()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var responseData = new
        {
            workflowId,
            workflowName = "TestWorkflow",
            startTime = DateTime.UtcNow,
            success = true,
            steps = new object[] { }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        var tempFile = Path.GetTempFileName();
        try
        {
            var workflowConfig = new { workflowName = "TestWorkflow" };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(workflowConfig, JsonOptions));

            // Act
            var command = new WorkflowExecuteCommand(_apiClient, _jsonHandler, _mockExecuteLogger.Object, _configuration);
            var result = await InvokeCommandAsync(command, new[] { "--file", tempFile, "--async" });

            // Assert
            result.Should().Be(0);
            _consoleOutput.ToString().Should().Contain(workflowId.ToString());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Tests workflow execute with failed workflow
    /// </summary>
    [Fact]
    public async Task WorkflowExecute_FailedWorkflow_ReturnsErrorCode()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var responseData = new
        {
            workflowId,
            workflowName = "FailedWorkflow",
            startTime = DateTime.UtcNow,
            endTime = DateTime.UtcNow.AddMinutes(1),
            duration = TimeSpan.FromMinutes(1),
            success = false,
            errorMessage = "Snapshot creation failed",
            steps = new[]
            {
                new
                {
                    stepName = "Create Baseline Snapshot",
                    startTime = DateTime.UtcNow,
                    endTime = DateTime.UtcNow.AddSeconds(10),
                    duration = TimeSpan.FromSeconds(10),
                    success = false,
                    message = "Failed to create snapshot"
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        var tempFile = Path.GetTempFileName();
        try
        {
            var workflowConfig = new { workflowName = "FailedWorkflow" };
            await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(workflowConfig, JsonOptions));

            // Act
            var command = new WorkflowExecuteCommand(_apiClient, _jsonHandler, _mockExecuteLogger.Object, _configuration);
            var result = await InvokeCommandAsync(command, new[] { "--file", tempFile });

            // Assert
            result.Should().Be(1); // API error for failed workflow
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    /// <summary>
    /// Tests workflow status retrieval
    /// </summary>
    [Fact]
    public async Task WorkflowStatus_ValidId_ReturnsStatus()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var responseData = new
        {
            workflowId,
            name = "TestWorkflow",
            status = "Running",
            currentPhase = "Execute Baseline Workload",
            progress = 45.5,
            startTime = DateTime.UtcNow.AddMinutes(-5),
            estimatedCompletion = DateTime.UtcNow.AddMinutes(5)
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        // Act
        var command = new WorkflowStatusCommand(_apiClient, _jsonHandler, _mockStatusLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { workflowId.ToString() });

        // Assert
        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain(workflowId.ToString());
        output.Should().Contain("Running");
    }

    /// <summary>
    /// Tests workflow status with invalid ID format
    /// </summary>
    [Fact]
    public async Task WorkflowStatus_InvalidIdFormat_ReturnsValidationError()
    {
        // Act
        var command = new WorkflowStatusCommand(_apiClient, _jsonHandler, _mockStatusLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { "invalid-guid" });

        // Assert
        result.Should().Be(3); // Validation error
        _consoleError.ToString().Should().Contain("Invalid workflow ID");
    }

    /// <summary>
    /// Tests workflow status with completed status
    /// </summary>
    [Fact]
    public async Task WorkflowStatus_CompletedWorkflow_ReturnsSuccess()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var responseData = new
        {
            workflowId,
            name = "CompletedWorkflow",
            status = "Completed",
            progress = 100.0,
            startTime = DateTime.UtcNow.AddMinutes(-10),
            duration = TimeSpan.FromMinutes(10)
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        // Act
        var command = new WorkflowStatusCommand(_apiClient, _jsonHandler, _mockStatusLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { workflowId.ToString() });

        // Assert
        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("Completed");
    }

    /// <summary>
    /// Tests workflow list with no filters
    /// </summary>
    [Fact]
    public async Task WorkflowList_NoFilters_ReturnsAllWorkflows()
    {
        // Arrange
        var responseData = new object[]
        {
            new
            {
                workflowId = Guid.NewGuid(),
                workflowName = "Workflow1",
                status = "Completed",
                startTime = DateTime.UtcNow.AddHours(-2),
                endTime = (DateTime?)DateTime.UtcNow.AddHours(-1),
                success = true,
                stepCount = 5
            },
            new
            {
                workflowId = Guid.NewGuid(),
                workflowName = "Workflow2",
                status = "Running",
                startTime = DateTime.UtcNow.AddMinutes(-30),
                endTime = (DateTime?)null,
                success = false,
                stepCount = 3
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, Array.Empty<string>());

        // Assert
        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("Workflow1");
        output.Should().Contain("Workflow2");
    }

    /// <summary>
    /// Tests workflow list with status filter
    /// </summary>
    [Fact]
    public async Task WorkflowList_StatusFilter_ReturnsFilteredWorkflows()
    {
        // Arrange
        var responseData = new[]
        {
            new
            {
                workflowId = Guid.NewGuid(),
                workflowName = "RunningWorkflow",
                status = "Running",
                startTime = DateTime.UtcNow.AddMinutes(-15),
                endTime = (DateTime?)null,
                success = false,
                stepCount = 2
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { "--status", "running" });

        // Assert
        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("RunningWorkflow");
    }

    /// <summary>
    /// Tests workflow list with empty results
    /// </summary>
    [Fact]
    public async Task WorkflowList_EmptyResults_ReturnsEmptyList()
    {
        // Arrange
        SetupMockHttpResponse(HttpStatusCode.OK, Array.Empty<object>());

        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, Array.Empty<string>());

        // Assert
        result.Should().Be(0);
        var output = _consoleOutput.ToString();
        output.Should().Contain("[]"); // Empty JSON array
    }

    /// <summary>
    /// Tests workflow list with invalid status
    /// </summary>
    [Fact]
    public async Task WorkflowList_InvalidStatus_ReturnsValidationError()
    {
        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { "--status", "invalid-status" });

        // Assert
        result.Should().Be(3); // Validation error
        _consoleError.ToString().Should().Contain("Invalid status");
    }

    /// <summary>
    /// Tests workflow list with limit option
    /// </summary>
    [Fact]
    public async Task WorkflowList_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var responseData = new[]
        {
            new
            {
                workflowId = Guid.NewGuid(),
                workflowName = "Workflow1",
                status = "Completed",
                startTime = DateTime.UtcNow.AddHours(-1),
                endTime = DateTime.UtcNow,
                success = true,
                stepCount = 5
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, responseData);

        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { "--limit", "10" });

        // Assert
        result.Should().Be(0);
    }

    /// <summary>
    /// Tests workflow list with invalid limit
    /// </summary>
    [Fact]
    public async Task WorkflowList_InvalidLimit_ReturnsValidationError()
    {
        // Act
        var command = new WorkflowListCommand(_apiClient, _jsonHandler, _mockListLogger.Object, _configuration);
        var result = await InvokeCommandAsync(command, new[] { "--limit", "1000" }); // Max is 500

        // Assert
        result.Should().Be(3); // Validation error
        _consoleError.ToString().Should().Contain("between 1 and 500");
    }

    /// <summary>
    /// Sets up mock HTTP response
    /// </summary>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="responseData">Response data object</param>
    private void SetupMockHttpResponse(HttpStatusCode statusCode, object responseData)
    {
        var responseJson = JsonSerializer.Serialize(responseData, JsonOptions);
        var responseMessage = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);
    }

    /// <summary>
    /// Invokes a command with specified arguments
    /// </summary>
    /// <param name="command">Command to invoke</param>
    /// <param name="args">Command arguments</param>
    /// <returns>Exit code</returns>
    private static async Task<int> InvokeCommandAsync(System.CommandLine.Command command, string[] args)
    {
        return await command.InvokeAsync(args);
    }
}
