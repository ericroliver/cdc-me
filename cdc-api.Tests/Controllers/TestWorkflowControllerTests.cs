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
                var mockSnapshotManager = new Mock<SnapshotManager>();
                var mockTraceManager = new Mock<TraceManager>();
                var mockReplayEngine = new Mock<ReplayEngine>();
                var mockCdcComparator = new Mock<CdcComparator>();
                var mockTraceDataProvider = new Mock<ITraceDataProvider>();

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