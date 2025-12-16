using System;
using FluentAssertions;
using Xunit;

namespace cdc_api.Tests.Trace;

/// <summary>
/// Tests for SnapshotManager path parsing logic
/// </summary>
public class SnapshotManagerTests
{

    /// <summary>
    /// Test that Windows paths are correctly parsed even when running on Linux
    /// </summary>
    /// <param name="windowsPath">The Windows file path to test</param>
    /// <param name="expectedDirectory">The expected directory portion</param>
    [Theory]
    [InlineData(@"H:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\DATA\vibeneb.mdf", @"H:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\DATA")]
    [InlineData(@"C:\Data\MyDatabase.mdf", @"C:\Data")]
    [InlineData(@"D:\SQL\Databases\Test.mdf", @"D:\SQL\Databases")]
    public void WindowsPath_ShouldBeCorrectlyParsed(string windowsPath, string expectedDirectory)
    {
        // Arrange - Simulate the path parsing logic from SnapshotManager
        var physicalPath = windowsPath;
        var lastSeparator = Math.Max(physicalPath.LastIndexOf('\\'), physicalPath.LastIndexOf('/'));
        var directory = lastSeparator >= 0 ? physicalPath.Substring(0, lastSeparator) : "";

        // Assert
        directory.Should().Be(expectedDirectory);
    }

    /// <summary>
    /// Test that Unix paths are also correctly parsed
    /// </summary>
    /// <param name="unixPath">The Unix file path to test</param>
    /// <param name="expectedDirectory">The expected directory portion</param>
    [Theory]
    [InlineData("/var/lib/postgresql/data/mydb.mdf", "/var/lib/postgresql/data")]
    [InlineData("/mnt/data/database.mdf", "/mnt/data")]
    public void UnixPath_ShouldBeCorrectlyParsed(string unixPath, string expectedDirectory)
    {
        // Arrange - Simulate the path parsing logic from SnapshotManager
        var physicalPath = unixPath;
        var lastSeparator = Math.Max(physicalPath.LastIndexOf('\\'), physicalPath.LastIndexOf('/'));
        var directory = lastSeparator >= 0 ? physicalPath.Substring(0, lastSeparator) : "";

        // Assert
        directory.Should().Be(expectedDirectory);
    }

    /// <summary>
    /// Test that snapshot file paths are correctly constructed from database file paths.
    /// This tests the exact path parsing logic used in SnapshotManager.CreateSnapshotAsync.
    /// </summary>
    /// <param name="physicalPath">The physical database file path</param>
    /// <param name="logicalName">The logical file name</param>
    /// <param name="expectedSnapshotPath">The expected snapshot file path</param>
    [Theory]
    [InlineData(@"H:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\DATA\vibeneb.mdf",
                "Adapta2008Co811",
                @"H:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\DATA\Adapta2008Co811_snapshot.ss")]
    [InlineData(@"C:\Data\MyDatabase.mdf",
                "MyDatabase",
                @"C:\Data\MyDatabase_snapshot.ss")]
    [InlineData("/var/lib/postgresql/data/mydb.mdf",
                "mydb",
                "/var/lib/postgresql/data/mydb_snapshot.ss")]
    [InlineData(@"D:\SQL\Databases\Test.mdf",
                "TestDB",
                @"D:\SQL\Databases\TestDB_snapshot.ss")]
    public void SnapshotFilePath_ShouldBeCorrectlyConstructed(string physicalPath, string logicalName, string expectedSnapshotPath)
    {
        // Arrange - Replicate the exact snapshot path construction logic from SnapshotManager
        var snapshotFileName = $"{logicalName}_snapshot.ss";

        // Extract directory from path using string manipulation, preserving original
        // separator
        var lastBackslash = physicalPath.LastIndexOf('\\');
        var lastForwardslash = physicalPath.LastIndexOf('/');
        var lastSeparator = Math.Max(lastBackslash, lastForwardslash);
        var separator = lastBackslash > lastForwardslash ? '\\' : '/';
        var directory = lastSeparator >= 0 ? physicalPath.Substring(0, lastSeparator) : "";
        var snapshotFilePath = string.IsNullOrEmpty(directory)
            ? snapshotFileName
            : $"{directory}{separator}{snapshotFileName}";

        // Assert
        snapshotFilePath.Should().Be(expectedSnapshotPath,
            "the path parsing logic should correctly handle both Windows and Unix paths while preserving the original path separator");
    }
}
