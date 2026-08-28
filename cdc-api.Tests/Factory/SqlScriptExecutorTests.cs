using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Executors;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlScriptExecutorTests
{
    private readonly Mock<IDatabaseProvider> _providerMock = new();
    private readonly SqlScriptExecutor _executor;

    public SqlScriptExecutorTests()
    {
        _executor = new SqlScriptExecutor(_providerMock.Object, NullLogger<SqlScriptExecutor>.Instance);
    }

    [Fact]
    public void Constructor_ThrowsWhenProviderNull()
    {
        var act = () => new SqlScriptExecutor(null!, NullLogger<SqlScriptExecutor>.Instance);
        act.Should().Throw<ArgumentNullException>().WithParameterName("databaseProvider");
    }

    [Fact]
    public void SubstituteParameters_ReplacesTokens()
    {
        var sql = "DECLARE @Count INT = ${BranchCount}; SELECT * FROM Branches WHERE Industry = '${Industry}';";
        var parameters = new Dictionary<string, object?>
        {
            ["BranchCount"] = 10,
            ["Industry"] = "HD"
        };

        var result = SqlScriptExecutor.SubstituteParameters(sql, parameters);

        result.Should().Contain("= 10");
        result.Should().Contain("'HD'");
        result.Should().NotContain("${BranchCount}");
        result.Should().NotContain("${Industry}");
    }

    [Fact]
    public void SubstituteParameters_NoParameters_ReturnsUnchanged()
    {
        var sql = "SELECT 1";
        var result = SqlScriptExecutor.SubstituteParameters(sql, new Dictionary<string, object?>());
        result.Should().Be(sql);
    }

    [Fact]
    public void SubstituteParameters_NullParameters_ReturnsUnchanged()
    {
        var sql = "SELECT 1";
        var result = SqlScriptExecutor.SubstituteParameters(sql, null!);
        result.Should().Be(sql);
    }

    [Fact]
    public void SubstituteParameters_CaseInsensitive()
    {
        var sql = "SELECT ${BRANCHCOUNT}";
        var parameters = new Dictionary<string, object?> { ["BranchCount"] = 10 };

        var result = SqlScriptExecutor.SubstituteParameters(sql, parameters);
        result.Should().Be("SELECT 10");
    }

    [Fact]
    public async Task ExecuteAsync_WithContent_ExecutesSql()
    {
        var script = new Script
        {
            Name = "test",
            Content = "SELECT 1",
            Type = "SqlScript"
        };

        _providerMock.Setup(p => p.ExecuteSqlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
                     .ReturnsAsync(SqlResult.Ok(5));

        var result = await _executor.ExecuteAsync(script, new Dictionary<string, object?>(), "Server=test;");

        result.Success.Should().BeTrue();
        result.ScriptName.Should().Be("test");
        result.RowsAffected.Should().Be(5);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoContentOrFilePath_ReturnsFailure()
    {
        var script = new Script { Name = "empty" };

        var result = await _executor.ExecuteAsync(script, new Dictionary<string, object?>(), "Server=test;");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no content or file path");
    }

    [Fact]
    public async Task ExecuteAsync_WithFilePath_ReadsAndExecutes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"script_{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, "SELECT 1");

        try
        {
            var script = new Script { Name = "file-script", FilePath = path };
            _providerMock.Setup(p => p.ExecuteSqlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
                         .ReturnsAsync(SqlResult.Ok(1));

            var result = await _executor.ExecuteAsync(script, new Dictionary<string, object?>(), "Server=test;");

            result.Success.Should().BeTrue();
            result.RowsAffected.Should().Be(1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProviderFailure_ReturnsFailure()
    {
        var script = new Script { Name = "fail", Content = "BAD SQL" };
        _providerMock.Setup(p => p.ExecuteSqlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
                     .ReturnsAsync(SqlResult.Fail("Syntax error"));

        var result = await _executor.ExecuteAsync(script, new Dictionary<string, object?>(), "Server=test;");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Syntax error");
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesParametersBeforeExecution()
    {
        var script = new Script { Name = "param", Content = "SELECT ${Count}" };
        var parameters = new Dictionary<string, object?> { ["Count"] = 42 };

        string? capturedSql = null;
        _providerMock.Setup(p => p.ExecuteSqlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, object?>?>()))
                     .Callback<string, string, IReadOnlyDictionary<string, object?>?>((_, sql, _) => capturedSql = sql)
                     .ReturnsAsync(SqlResult.Ok(1));

        await _executor.ExecuteAsync(script, parameters, "Server=test;");

        capturedSql.Should().Be("SELECT 42");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenScriptNull()
    {
        var act = () => _executor.ExecuteAsync(null!, new Dictionary<string, object?>(), "cs");
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("script");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenConnectionStringEmpty()
    {
        var script = new Script { Name = "test", Content = "SELECT 1" };
        var act = () => _executor.ExecuteAsync(script, new Dictionary<string, object?>(), "");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
