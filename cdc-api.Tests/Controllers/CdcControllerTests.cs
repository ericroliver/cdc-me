using System.Collections.Generic;
using System.Threading.Tasks;
using cdc_api.Controllers;
using cdc_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Softbase;
using Softbase.Cdc.Data;
using Xunit;

namespace cdc_api.Tests.Controllers;

/// <summary>
/// Unit tests for CdcController
/// </summary>
public class CdcControllerTests
{
    private readonly Mock<ILogger<CdcController>> _mockLogger;
    private readonly Mock<IDatabaseConnectionFactory> _mockConnectionFactory;

    /// <summary>
    /// Initializes test fixtures
    /// </summary>
    public CdcControllerTests()
    {
        _mockLogger = new Mock<ILogger<CdcController>>();
        _mockConnectionFactory = new Mock<IDatabaseConnectionFactory>();
    }

    /// <summary>
    /// Test that constructor throws ArgumentNullException for null logger
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CdcController(null!, _mockConnectionFactory.Object));
    }

    /// <summary>
    /// Test that constructor throws ArgumentNullException for null connection factory
    /// </summary>
    [Fact]
    public void Constructor_NullConnectionFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CdcController(_mockLogger.Object, null!));
    }

    /// <summary>
    /// Test that controller can be instantiated with valid parameters
    /// </summary>
    [Fact]
    public void Constructor_ValidParameters_CreatesController()
    {
        // Act
        var controller = new CdcController(_mockLogger.Object, _mockConnectionFactory.Object);

        // Assert
        Assert.NotNull(controller);
    }

    /// <summary>
    /// Test that table filtering logic works correctly with include filters
    /// </summary>
    [Fact]
    public void FilterTables_WithIncludeFilter_ReturnsFilteredTables()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers"),
            new SqlTable("TestDB", "dbo", "AuditLog")
        };

        var tablesToInclude = new List<string> { "dbo.Orders", "dbo.Customers" };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, tablesToInclude, null);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, t => t.Name == "Orders");
        Assert.Contains(result, t => t.Name == "Customers");
        Assert.DoesNotContain(result, t => t.Name == "AuditLog");
    }

    /// <summary>
    /// Test that table filtering logic works correctly with exclude filters
    /// </summary>
    [Fact]
    public void FilterTables_WithExcludeFilter_ReturnsFilteredTables()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers"),
            new SqlTable("TestDB", "dbo", "AuditLog")
        };

        var tablesToExclude = new List<string> { "dbo.AuditLog" };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, null, tablesToExclude);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, t => t.Name == "Orders");
        Assert.Contains(result, t => t.Name == "Customers");
        Assert.DoesNotContain(result, t => t.Name == "AuditLog");
    }

    /// <summary>
    /// Test that table filtering logic works correctly with both include and exclude filters
    /// </summary>
    [Fact]
    public void FilterTables_WithIncludeAndExcludeFilters_ReturnsCorrectTables()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers"),
            new SqlTable("TestDB", "dbo", "Products"),
            new SqlTable("TestDB", "dbo", "AuditLog")
        };

        var tablesToInclude = new List<string> { "dbo.Orders", "dbo.Customers", "dbo.AuditLog" };
        var tablesToExclude = new List<string> { "dbo.AuditLog" };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, tablesToInclude, tablesToExclude);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, t => t.Name == "Orders");
        Assert.Contains(result, t => t.Name == "Customers");
        Assert.DoesNotContain(result, t => t.Name == "Products");
        Assert.DoesNotContain(result, t => t.Name == "AuditLog");
    }

    /// <summary>
    /// Test that table filtering is case insensitive
    /// </summary>
    [Fact]
    public void FilterTables_CaseInsensitive_ReturnsCorrectTables()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers")
        };

        var tablesToInclude = new List<string> { "DBO.ORDERS", "dbo.customers" };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, tablesToInclude, null);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, t => t.Name == "Orders");
        Assert.Contains(result, t => t.Name == "Customers");
    }

    /// <summary>
    /// Test that table filtering with no filters returns all tables
    /// </summary>
    [Fact]
    public void FilterTables_NoFilters_ReturnsAllTables()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers"),
            new SqlTable("TestDB", "dbo", "Products")
        };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, null, null);

        // Assert
        Assert.Equal(3, result.Count());
        Assert.Contains(result, t => t.Name == "Orders");
        Assert.Contains(result, t => t.Name == "Customers");
        Assert.Contains(result, t => t.Name == "Products");
    }

    /// <summary>
    /// Test that table filtering with empty lists returns all tables
    /// </summary>
    [Fact]
    public void FilterTables_EmptyLists_ReturnsAllTables()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers")
        };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, new List<string>(), new List<string>());

        // Assert
        Assert.Equal(2, result.Count());
    }

    /// <summary>
    /// Test that table filtering with non-matching include filter returns empty
    /// </summary>
    [Fact]
    public void FilterTables_NonMatchingInclude_ReturnsEmpty()
    {
        // Arrange
        var allTables = new List<SqlTable>
        {
            new SqlTable("TestDB", "dbo", "Orders"),
            new SqlTable("TestDB", "dbo", "Customers")
        };

        var tablesToInclude = new List<string> { "dbo.NonExistent" };

        // Act
        var result = CdcControllerTestHelper.FilterTablesPublic(allTables, tablesToInclude, null);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Test that CompareCapturesAsync returns BadRequest for invalid model state
    /// </summary>
    [Fact]
    public async Task CompareCapturesAsync_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var controller = new CdcController(_mockLogger.Object, _mockConnectionFactory.Object);
        controller.ModelState.AddModelError("BaselineCaptureName", "Required");

        var request = new CompareCapturesRequest();

        // Act
        var result = await controller.CompareCapturesAsync(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Test that CompareCapturesAsync validates required fields
    /// </summary>
    [Fact]
    public async Task CompareCapturesAsync_MissingRequiredFields_ReturnsBadRequest()
    {
        // Arrange
        var controller = new CdcController(_mockLogger.Object, _mockConnectionFactory.Object);
        var request = new CompareCapturesRequest
        {
            BaselineCaptureName = "", // Empty required field
            TestCaptureName = "test"
        };

        // Manually add model error to simulate validation
        controller.ModelState.AddModelError("BaselineCaptureName", "The BaselineCaptureName field is required.");

        // Act
        var result = await controller.CompareCapturesAsync(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    /// <summary>
    /// Test that CompareCapturesAsync request model has correct structure
    /// </summary>
    [Fact]
    public void CompareCapturesRequest_HasCorrectStructure()
    {
        // Arrange & Act
        var request = new CompareCapturesRequest
        {
            BaselineCaptureName = "baseline",
            TestCaptureName = "test",
            FieldsToIgnore = new List<string> { "created_date" },
            IgnoreLsnDifferences = true
        };

        // Assert
        Assert.Equal("baseline", request.BaselineCaptureName);
        Assert.Equal("test", request.TestCaptureName);
        Assert.NotNull(request.FieldsToIgnore);
        Assert.Single(request.FieldsToIgnore);
        Assert.True(request.IgnoreLsnDifferences);
    }

    /// <summary>
    /// Test that CompareCapturesResponse model has correct structure
    /// </summary>
    [Fact]
    public void CompareCapturesResponse_HasCorrectStructure()
    {
        // Arrange & Act
        var response = new CompareCapturesResponse
        {
            IsMatch = false,
            Failures = new List<CaptureComparisonFailure>
            {
                new()
                {
                    TableName = "Orders",
                    FailureType = "FieldMismatch",
                    PrimaryKey = "123",
                    FieldName = "amount",
                    BaselineValue = 100,
                    TestValue = 200,
                    Description = "Amount mismatch"
                }
            },
            Summary = new cdc_api.Models.ComparisonSummary
            {
                TablesCompared = 1,
                RecordsCompared = 10,
                FieldsCompared = 50,
                TotalFailures = 1,
                TablesWithFailures = 1,
                ComparisonDuration = TimeSpan.FromSeconds(1)
            },
            Errors = new List<string>()
        };

        // Assert
        Assert.False(response.IsMatch);
        Assert.Single(response.Failures);
        Assert.Equal("Orders", response.Failures[0].TableName);
        Assert.Equal("FieldMismatch", response.Failures[0].FailureType);
        Assert.NotNull(response.Summary);
        Assert.Equal(1, response.Summary.TablesCompared);
        Assert.Empty(response.Errors);
    }

    /// <summary>
    /// Test that CompareCapturesRequest supports optional FieldsToIgnore
    /// </summary>
    [Fact]
    public void CompareCapturesRequest_FieldsToIgnore_IsOptional()
    {
        // Arrange & Act
        var request = new CompareCapturesRequest
        {
            BaselineCaptureName = "baseline",
            TestCaptureName = "test"
            // FieldsToIgnore not set
        };

        // Assert
        Assert.Null(request.FieldsToIgnore);
    }

    /// <summary>
    /// Test that CompareCapturesRequest has correct default for IgnoreLsnDifferences
    /// </summary>
    [Fact]
    public void CompareCapturesRequest_IgnoreLsnDifferences_DefaultsToTrue()
    {
        // Arrange & Act
        var request = new CompareCapturesRequest();

        // Assert
        Assert.True(request.IgnoreLsnDifferences);
    }

    /// <summary>
    /// Test that CaptureComparisonFailure supports all failure types
    /// </summary>
    [Theory]
    [InlineData("MissingTable")]
    [InlineData("ExtraTable")]
    [InlineData("MissingRecord")]
    [InlineData("ExtraRecord")]
    [InlineData("FieldMismatch")]
    [InlineData("OperationMismatch")]
    [InlineData("RecordCountMismatch")]
    public void CaptureComparisonFailure_SupportsAllFailureTypes(string failureType)
    {
        // Arrange & Act
        var failure = new CaptureComparisonFailure
        {
            FailureType = failureType,
            TableName = "TestTable",
            Description = $"Test {failureType}"
        };

        // Assert
        Assert.Equal(failureType, failure.FailureType);
        Assert.Equal("TestTable", failure.TableName);
    }

    /// <summary>
    /// Test that ComparisonSummary initializes with zero values
    /// </summary>
    [Fact]
    public void ComparisonSummary_InitializesWithZeroValues()
    {
        // Arrange & Act
        var summary = new cdc_api.Models.ComparisonSummary();

        // Assert
        Assert.Equal(0, summary.TablesCompared);
        Assert.Equal(0, summary.RecordsCompared);
        Assert.Equal(0, summary.FieldsCompared);
        Assert.Equal(0, summary.TotalFailures);
        Assert.Equal(0, summary.TablesWithFailures);
        Assert.Equal(TimeSpan.Zero, summary.ComparisonDuration);
    }

    /// <summary>
    /// Test that CompareCapturesResponse handles empty failures list
    /// </summary>
    [Fact]
    public void CompareCapturesResponse_EmptyFailures_IndicatesMatch()
    {
        // Arrange & Act
        var response = new CompareCapturesResponse
        {
            IsMatch = true,
            Failures = new List<CaptureComparisonFailure>(),
            Summary = new cdc_api.Models.ComparisonSummary
            {
                TablesCompared = 5,
                RecordsCompared = 100
            }
        };

        // Assert
        Assert.True(response.IsMatch);
        Assert.Empty(response.Failures);
        Assert.Equal(5, response.Summary.TablesCompared);
    }

    /// <summary>
    /// Test that CompareCapturesResponse can include error messages
    /// </summary>
    [Fact]
    public void CompareCapturesResponse_CanIncludeErrors()
    {
        // Arrange & Act
        var response = new CompareCapturesResponse
        {
            IsMatch = false,
            Errors = new List<string>
            {
                "Baseline capture not found",
                "Database connection failed"
            }
        };

        // Assert
        Assert.False(response.IsMatch);
        Assert.Equal(2, response.Errors.Count);
        Assert.Contains("Baseline capture not found", response.Errors);
    }
}

/// <summary>
/// Helper class to expose private methods for testing
/// </summary>
public static class CdcControllerTestHelper
{
    /// <summary>
    /// Public wrapper for the private FilterTables method
    /// </summary>
    /// <param name="allTables">All available tables</param>
    /// <param name="tablesToInclude">Tables to include</param>
    /// <param name="tablesToExclude">Tables to exclude</param>
    /// <returns>Filtered tables</returns>
    public static IEnumerable<SqlTable> FilterTablesPublic(
        IEnumerable<SqlTable> allTables,
        List<string>? tablesToInclude,
        List<string>? tablesToExclude)
    {
        // Use the controller's FilterTables method to avoid duplication
        return CdcController.FilterTables(allTables, tablesToInclude, tablesToExclude);
    }
}
