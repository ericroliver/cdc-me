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

public class TestWorkflowControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TestWorkflowControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real services with mocks for testing
                var mockSnapshotManager = new Mock<ISnapshotManager>();
                var mockTraceManager = new Mock<ITraceManager>();
                var mockReplayEngine = new Mock<IReplayEngine>();
                var mockCdcComparator = new Mock<ICdcComparator>();
                var mockTraceDataProvider = new Mock<ITraceDataProvider>();

                // Setup mock returns for successful operations
                var testSession = new TraceSession
                {
                    SessionId = Guid.NewGuid(),
                    SessionName = "TestTraceSession",
                    DatabaseName = "TestDB",
                    Status = TraceStatus.Running,
                    StartTime = DateTime.UtcNow,
                    Configuration = new TraceConfiguration()
                };

                mockSnapshotManager.Setup(x => x.CreateSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot created successfully" });

                mockSnapshotManager.Setup(x => x.RestoreSnapshotAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new SnapshotResult { Success = true, Message = "Snapshot restored successfully" });

                mockTraceManager.Setup(x => x.StartTraceAsync(It.IsAny<TraceConfiguration>()))
                    .ReturnsAsync(testSession);

                mockTraceManager.Setup(x => x.StopTraceAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(testSession);

                mockTraceManager.Setup(x => x.ExportTraceDataAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                    .ReturnsAsync("/path/to/export");

                mockTraceDataProvider.Setup(x => x.CreateTraceSessionAsync(It.IsAny<TraceSession>()))
                    .Returns(Task.CompletedTask);

                mockTraceDataProvider.Setup(x => x.GetTraceSessionAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(testSession);

                mockTraceDataProvider.Setup(x => x.UpdateTraceSessionAsync(It.IsAny<TraceSession>()))
                    .Returns(Task.CompletedTask);

                mockReplayEngine.Setup(x => x.ReplayTraceAsync(It.IsAny<Guid>(), It.IsAny<ReplayOptions>()))
                    .ReturnsAsync(new ReplayResult
                    {
                        SessionId = testSession.SessionId,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddMinutes(1),
                        TotalStatements = 10,
                        SuccessfulStatements = 10,
                        FailedStatements = 0
                    });

                mockReplayEngine.Setup(x => x.ExecuteStatementsFromFileAsync(It.IsAny<string>(), It.IsAny<ReplayOptions>()))
                    .ReturnsAsync(new ReplayResult
                    {
                        SessionId = testSession.SessionId,
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddMinutes(1),
                        TotalStatements = 5,
                        SuccessfulStatements = 5,
                        FailedStatements = 0
                    });

                mockCdcComparator.Setup(x => x.CompareCdcDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ComparisonConfiguration>()))
                    .ReturnsAsync(new ComparisonResult
                    {
                        ComparisonId = Guid.NewGuid(),
                        SessionId = testSession.SessionId,
                        ComparisonTime = DateTime.UtcNow,
                        OverallMatch = true,
                        TotalDifferences = 0
                    });

                services.AddSingleton(mockSnapshotManager.Object);
                services.AddSingleton(mockTraceManager.Object);
                services.AddSingleton(mockReplayEngine.Object);
                services.AddSingleton(mockCdcComparator.Object);
                services.AddSingleton(mockTraceDataProvider.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task ExecuteWorkflow_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new WorkflowExecutionRequest
        {
            WorkflowName = "TestWorkflow",
            DatabaseName = "TestDB",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;",
            TraceConnectionString = "Server=test;Database=trace;Trusted_Connection=true;",
            BaselineSnapshotName = "BaselineSnapshot",
            TestSnapshotName = "TestSnapshot",
            TraceSessionName = "TestTraceSession",
            EnableCdc = true,
            CdcTables = new List<string> { "dbo.TestTable1", "dbo.TestTable2" },
            TraceConfig = new TraceConfiguration
            {
                MaxFileSize = 100,
                MaxFiles = 5,
                EventsToCapture = new List<string> { "sql_statement_completed" }
            },
            ComparisonConfig = new ComparisonConfiguration
            {
                DateTimeToleranceWindow = TimeSpan.FromMinutes(5),
                ExcludedColumns = new[] { "__$start_lsn", "__$end_lsn" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/testworkflow/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExecuteWorkflow_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new WorkflowExecutionRequest
        {
            WorkflowName = "",
            DatabaseName = "",
            ConnectionString = "",
            TraceConnectionString = "",
            BaselineSnapshotName = "",
            TestSnapshotName = "",
            TraceSessionName = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/testworkflow/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetWorkflowStatus_ValidRequest_ReturnsOk()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/testworkflow/status/{workflowId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListWorkflowExecutions_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/testworkflow/executions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExecuteWorkflow_MinimalRequest_ReturnsOk()
    {
        // Arrange
        var request = new WorkflowExecutionRequest
        {
            WorkflowName = "MinimalWorkflow",
            DatabaseName = "TestDB",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;",
            TraceConnectionString = "Server=test;Database=trace;Trusted_Connection=true;",
            BaselineSnapshotName = "BaselineSnapshot",
            TestSnapshotName = "TestSnapshot",
            TraceSessionName = "TestTraceSession",
            EnableCdc = false
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/testworkflow/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExecuteWorkflow_WithBaselineWorkload_ReturnsOk()
    {
        // Arrange
        var request = new WorkflowExecutionRequest
        {
            WorkflowName = "WorkflowWithBaseline",
            DatabaseName = "TestDB",
            ConnectionString = "Server=test;Database=test;Trusted_Connection=true;",
            TraceConnectionString = "Server=test;Database=trace;Trusted_Connection=true;",
            BaselineSnapshotName = "BaselineSnapshot",
            TestSnapshotName = "TestSnapshot",
            TraceSessionName = "TestTraceSession",
            EnableCdc = true,
            BaselineWorkloadPath = "/path/to/baseline/workload.sql",
            CdcTables = new List<string> { "dbo.TestTable" }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/testworkflow/execute", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}