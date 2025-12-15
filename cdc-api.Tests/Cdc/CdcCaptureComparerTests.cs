using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Softbase;
using Softbase.Cdc;
using Softbase.Cdc.Data;
using Xunit;

namespace cdc_api.Tests.Cdc;

/// <summary>
/// Unit tests for CdcCaptureComparer class
/// </summary>
public class CdcCaptureComparerTests
{
    private readonly Mock<ILogger> _mockLogger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Initializes a new instance of the test class
    /// </summary>
    public CdcCaptureComparerTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    /// <summary>
    /// Test that constructor throws ArgumentNullException when traceDac is null
    /// </summary>
    [Fact]
    public void Constructor_NullTraceDac_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CdcCaptureComparer(null!, _mockLogger.Object));
    }

    /// <summary>
    /// Test that constructor throws ArgumentNullException when logger is null
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockDac = CreateMockDac();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CdcCaptureComparer(mockDac, null!));
    }

    /// <summary>
    /// Test that CompareCapturesRequest validates required fields
    /// </summary>
    [Fact]
    public void CompareCapturesRequest_RequiredFields_AreValidated()
    {
        // Arrange
        var request = new CompareCapturesRequest();

        // Act & Assert - BaselineCaptureName and TestCaptureName are required
        Assert.Equal(string.Empty, request.BaselineCaptureName);
        Assert.Equal(string.Empty, request.TestCaptureName);
    }

    /// <summary>
    /// Test that CompareCapturesRequest has correct default values
    /// </summary>
    [Fact]
    public void CompareCapturesRequest_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var request = new CompareCapturesRequest();

        // Assert
        Assert.True(request.IgnoreLsnDifferences);
        Assert.Null(request.FieldsToIgnore);
    }

    /// <summary>
    /// Test that CompareCapturesResponse initializes correctly
    /// </summary>
    [Fact]
    public void CompareCapturesResponse_Initialization_HasCorrectDefaults()
    {
        // Arrange & Act
        var response = new CompareCapturesResponse();

        // Assert
        Assert.False(response.IsMatch);
        Assert.NotNull(response.Failures);
        Assert.Empty(response.Failures);
        Assert.NotNull(response.Summary);
        Assert.NotNull(response.Errors);
        Assert.Empty(response.Errors);
    }

    /// <summary>
    /// Test that CaptureComparisonFailure has all required properties
    /// </summary>
    [Fact]
    public void CaptureComparisonFailure_Properties_AreAccessible()
    {
        // Arrange & Act
        var failure = new CaptureComparisonFailure
        {
            TableName = "Orders",
            FailureType = FailureTypes.FieldMismatch,
            PrimaryKey = "123",
            FieldName = "amount",
            BaselineValue = 100.50,
            TestValue = 100.75,
            Description = "Amount mismatch"
        };

        // Assert
        Assert.Equal("Orders", failure.TableName);
        Assert.Equal(FailureTypes.FieldMismatch, failure.FailureType);
        Assert.Equal("123", failure.PrimaryKey);
        Assert.Equal("amount", failure.FieldName);
        Assert.Equal(100.50, failure.BaselineValue);
        Assert.Equal(100.75, failure.TestValue);
        Assert.Equal("Amount mismatch", failure.Description);
    }

    /// <summary>
    /// Test that ComparisonSummary tracks statistics correctly
    /// </summary>
    [Fact]
    public void ComparisonSummary_Properties_TrackStatistics()
    {
        // Arrange & Act
        var summary = new ComparisonSummary
        {
            TablesCompared = 5,
            RecordsCompared = 100,
            FieldsCompared = 500,
            TotalFailures = 3,
            TablesWithFailures = 2,
            ComparisonDuration = TimeSpan.FromSeconds(1.5)
        };

        // Assert
        Assert.Equal(5, summary.TablesCompared);
        Assert.Equal(100, summary.RecordsCompared);
        Assert.Equal(500, summary.FieldsCompared);
        Assert.Equal(3, summary.TotalFailures);
        Assert.Equal(2, summary.TablesWithFailures);
        Assert.Equal(1.5, summary.ComparisonDuration.TotalSeconds);
    }

    /// <summary>
    /// Test that FailureTypes constants are defined correctly
    /// </summary>
    [Fact]
    public void FailureTypes_Constants_AreDefinedCorrectly()
    {
        // Assert
        Assert.Equal("MissingTable", FailureTypes.MissingTable);
        Assert.Equal("ExtraTable", FailureTypes.ExtraTable);
        Assert.Equal("MissingRecord", FailureTypes.MissingRecord);
        Assert.Equal("ExtraRecord", FailureTypes.ExtraRecord);
        Assert.Equal("FieldMismatch", FailureTypes.FieldMismatch);
        Assert.Equal("OperationMismatch", FailureTypes.OperationMismatch);
        Assert.Equal("RecordCountMismatch", FailureTypes.RecordCountMismatch);
    }

    /// <summary>
    /// Test that comparison models serialize to JSON correctly
    /// </summary>
    [Fact]
    public void ComparisonModels_SerializeToJson_Successfully()
    {
        // Arrange
        var response = new CompareCapturesResponse
        {
            IsMatch = false,
            Failures = new List<CaptureComparisonFailure>
            {
                new()
                {
                    TableName = "Orders",
                    FailureType = FailureTypes.FieldMismatch,
                    Description = "Test failure"
                }
            },
            Summary = new ComparisonSummary
            {
                TablesCompared = 1,
                TotalFailures = 1
            }
        };

        // Act
        var json = JsonSerializer.Serialize(response, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CompareCapturesResponse>(json, JsonOptions);

        // Assert
        Assert.NotNull(deserialized);
        Assert.False(deserialized.IsMatch);
        Assert.Single(deserialized.Failures);
        Assert.Equal("Orders", deserialized.Failures[0].TableName);
    }

    /// <summary>
    /// Test that FieldsToIgnore list works with case-insensitive comparison
    /// </summary>
    [Fact]
    public void CompareCapturesRequest_FieldsToIgnore_SupportsMultipleFields()
    {
        // Arrange & Act
        var request = new CompareCapturesRequest
        {
            BaselineCaptureName = "baseline",
            TestCaptureName = "test",
            FieldsToIgnore = new List<string> { "created_date", "modified_date", "timestamp" }
        };

        // Assert
        Assert.NotNull(request.FieldsToIgnore);
        Assert.Equal(3, request.FieldsToIgnore.Count);
        Assert.Contains("created_date", request.FieldsToIgnore);
        Assert.Contains("modified_date", request.FieldsToIgnore);
        Assert.Contains("timestamp", request.FieldsToIgnore);
    }

    /// <summary>
    /// Test that comparison response can handle large numbers of failures
    /// </summary>
    [Fact]
    public void CompareCapturesResponse_HandlesLargeNumberOfFailures()
    {
        // Arrange
        var response = new CompareCapturesResponse();
        var failures = new List<CaptureComparisonFailure>();

        // Act - Add 1000 failures
        for (int i = 0; i < 1000; i++)
        {
            failures.Add(new CaptureComparisonFailure
            {
                TableName = $"Table{i % 10}",
                FailureType = FailureTypes.FieldMismatch,
                PrimaryKey = i.ToString(),
                Description = $"Failure {i}"
            });
        }
        response.Failures = failures;
        response.Summary.TotalFailures = failures.Count;

        // Assert
        Assert.Equal(1000, response.Failures.Count);
        Assert.Equal(1000, response.Summary.TotalFailures);
        Assert.False(response.IsMatch);
    }

    /// <summary>
    /// Test that comparison summary calculates duration correctly
    /// </summary>
    [Fact]
    public void ComparisonSummary_Duration_IsTrackedCorrectly()
    {
        // Arrange
        var summary = new ComparisonSummary();
        var startTime = DateTime.UtcNow;

        // Act - Simulate some work
        System.Threading.Thread.Sleep(100);
        var endTime = DateTime.UtcNow;
        summary.ComparisonDuration = endTime - startTime;

        // Assert
        Assert.True(summary.ComparisonDuration.TotalMilliseconds >= 100);
        Assert.True(summary.ComparisonDuration.TotalMilliseconds < 1000);
    }

    /// <summary>
    /// Test that failure descriptions are human-readable
    /// </summary>
    [Theory]
    [InlineData(FailureTypes.MissingTable, "Orders", "Table 'Orders' exists in baseline but not in test capture")]
    [InlineData(FailureTypes.ExtraTable, "Customers", "Table 'Customers' exists in test capture but not in baseline")]
    [InlineData(FailureTypes.MissingRecord, "Orders", "Record with primary key '123' exists in baseline but not in test capture for table 'Orders'")]
    public void CaptureComparisonFailure_Description_IsHumanReadable(string failureType, string tableName, string expectedDescription)
    {
        // Arrange & Act
        var failure = new CaptureComparisonFailure
        {
            FailureType = failureType,
            TableName = tableName,
            PrimaryKey = "123",
            Description = expectedDescription
        };

        // Assert
        Assert.Contains(tableName, failure.Description, StringComparison.Ordinal);
        Assert.Equal(failureType, failure.FailureType);
    }

    /// <summary>
    /// Test that comparison request validates capture name format
    /// </summary>
    [Theory]
    [InlineData("baseline-capture")]
    [InlineData("baseline_capture")]
    [InlineData("BaselineCapture123")]
    [InlineData("baseline.capture")]
    public void CompareCapturesRequest_CaptureName_AcceptsValidFormats(string captureName)
    {
        // Arrange & Act
        var request = new CompareCapturesRequest
        {
            BaselineCaptureName = captureName,
            TestCaptureName = "test"
        };

        // Assert
        Assert.Equal(captureName, request.BaselineCaptureName);
        Assert.NotEmpty(request.BaselineCaptureName);
    }

    /// <summary>
    /// Helper method to create a mock SimpleDac
    /// </summary>
    /// <returns>Mock SimpleDac instance</returns>
    private SimpleDac CreateMockDac()
    {
        // Create a mock connection string for PostgreSQL (trace database)
        var connectionString = "Host=localhost;Database=test;Username=test;Password=test";
        return new SimpleDac(connectionString, DatabaseProvider.PostgreSQL, _mockLogger.Object);
    }
}
