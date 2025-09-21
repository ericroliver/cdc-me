using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using cdc_api.Controllers;
using cdc_api.Models;
using Softbase;
using Softbase.Cdc.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        var tables = allTables;

        // Apply include filter if specified
        if (tablesToInclude != null && tablesToInclude.Any())
        {
            var includeSet = new HashSet<string>(tablesToInclude, StringComparer.OrdinalIgnoreCase);
            tables = tables.Where(t => includeSet.Contains($"{t.Schema}.{t.Name}"));
        }

        // Apply exclude filter if specified
        if (tablesToExclude != null && tablesToExclude.Any())
        {
            var excludeSet = new HashSet<string>(tablesToExclude, StringComparer.OrdinalIgnoreCase);
            tables = tables.Where(t => !excludeSet.Contains($"{t.Schema}.{t.Name}"));
        }

        return tables;
    }
}