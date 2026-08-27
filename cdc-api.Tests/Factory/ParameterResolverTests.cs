using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Engine;
using Softbase.Cdc.Factory.Models;
using Xunit;

namespace cdc_api.Tests.Factory;

public class DependencyValidatorTests
{
    private static ScriptGroup MakeGroup(
        Guid? id = null,
        string name = "group",
        int layer = 0,
        params Guid[] deps) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        Layer = layer,
        Dependencies = deps
    };

    [Fact]
    public void Validate_NoDependencies_ReturnsValid()
    {
        var groups = new List<ScriptGroup>
        {
            MakeGroup(name: "a"),
            MakeGroup(name: "b")
        };

        var result = DependencyValidator.Validate(groups);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidDependencies_ReturnsValid()
    {
        var a = MakeGroup(name: "a", layer: 0);
        var b = MakeGroup(name: "b", layer: 1, deps: a.Id);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a, b });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingDependency_ReturnsError()
    {
        var missingId = Guid.NewGuid();
        var a = MakeGroup(name: "a", layer: 0, deps: missingId);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("unknown group"));
    }

    [Fact]
    public void Validate_CircularDependency_ReturnsError()
    {
        var a = MakeGroup(name: "a", layer: 0);
        var b = MakeGroup(name: "b", layer: 0, deps: a.Id);
        a = MakeGroup(id: a.Id, name: "a", layer: 0, deps: b.Id);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a, b });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void Validate_SelfReferencing_ReturnsError()
    {
        var a = MakeGroup(name: "a", layer: 0, deps: Guid.NewGuid());
        a = MakeGroup(id: a.Id, name: "a", layer: 0, deps: a.Id);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void Validate_DependencyInHigherLayer_ReturnsError()
    {
        var a = MakeGroup(name: "a", layer: 1);
        var b = MakeGroup(name: "b", layer: 0, deps: a.Id);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a, b });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("higher layer"));
    }

    [Fact]
    public void Validate_DependencyInSameLayer_ReturnsValid()
    {
        var a = MakeGroup(name: "a", layer: 1);
        var b = MakeGroup(name: "b", layer: 1, deps: a.Id);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a, b });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ThreeNodeCycle_ReturnsError()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();

        var a = MakeGroup(id: aId, name: "a", layer: 0, deps: cId);
        var b = MakeGroup(id: bId, name: "b", layer: 0, deps: aId);
        var c = MakeGroup(id: cId, name: "c", layer: 0, deps: bId);

        var result = DependencyValidator.Validate(new List<ScriptGroup> { a, b, c });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void Validate_EmptyList_ReturnsValid()
    {
        var result = DependencyValidator.Validate(new List<ScriptGroup>());

        result.IsValid.Should().BeTrue();
    }
}

public class ParameterResolverTests
{
    private readonly ParameterResolver _resolver = new(
        NullLogger<ParameterResolver>.Instance);

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new ParameterResolver(null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MergeParameters_InlineOnly()
    {
        var inline = new Dictionary<string, object?> { ["A"] = 1, ["B"] = "hello" };
        var result = _resolver.MergeParameters(inline, null);

        result["A"].Should().Be(1);
        result["B"].Should().Be("hello");
    }

    [Fact]
    public void MergeParameters_FileOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"params_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"A": 10, "B": "world"}""");

        try
        {
            var result = _resolver.MergeParameters(null, path);
            result["A"].Should().Be(10);
            result["B"].Should().Be("world");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MergeParameters_InlineOverridesFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"params_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"A": 10, "B": "world"}""");

        try
        {
            var inline = new Dictionary<string, object?> { ["A"] = 99 };
            var result = _resolver.MergeParameters(inline, path);

            result["A"].Should().Be(99);
            result["B"].Should().Be("world");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MergeParameters_BothNull_ReturnsEmpty()
    {
        var result = _resolver.MergeParameters(null, null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromFile_Json_DeserializesCorrectly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"params_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{"BranchCount": 10, "Industry": "HD", "CompanyName": "Acme"}""");

        try
        {
            var result = _resolver.LoadFromFile(path);
            result["BranchCount"].Should().Be(10);
            result["Industry"].Should().Be("HD");
            result["CompanyName"].Should().Be("Acme");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_Yaml_DeserializesCorrectly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"params_{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, "BranchCount: 10\nIndustry: HD\nCompanyName: Acme\n");

        try
        {
            var result = _resolver.LoadFromFile(path);
            result["BranchCount"].Should().Be(10);
            result["Industry"].Should().Be("HD");
            result["CompanyName"].Should().Be("Acme");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_Yml_Extension_Works()
    {
        var path = Path.Combine(Path.GetTempPath(), $"params_{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, "Key: Value\n");

        try
        {
            var result = _resolver.LoadFromFile(path);
            result["Key"].Should().Be("Value");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_UnsupportedFormat_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"params_{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<xml/>");

        try
        {
            var act = () => _resolver.LoadFromFile(path);
            act.Should().Throw<NotSupportedException>()
               .WithMessage("*Unsupported*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_NotFound_Throws()
    {
        var act = () => _resolver.LoadFromFile("/nonexistent/path/file.json");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ResolveDatabaseName_ReplacesTokens()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["CompanyName"] = "Acme",
            ["Industry"] = "HD",
            ["templateVersion"] = "14.3.5"
        };

        var result = _resolver.ResolveDatabaseName(
            "{CompanyName}_{Industry}_v{templateVersion}_{date}",
            parameters);

        result.Should().Contain("Acme_HD_v14.3.5");
        result.Should().NotContain("{CompanyName}");
        result.Should().NotContain("{Industry}");
        result.Should().NotContain("{templateVersion}");
    }

    [Fact]
    public void ResolveDatabaseName_DateToken_Replaced()
    {
        var result = _resolver.ResolveDatabaseName("db_{date}", new Dictionary<string, object?>());
        result.Should().NotContain("{date}");
        result.Should().StartWith("db_");
    }

    [Fact]
    public void ResolveDatabaseName_EmptyTemplate_ReturnsEmpty()
    {
        var result = _resolver.ResolveDatabaseName("", new Dictionary<string, object?>());
        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveDatabaseName_NoMatchingToken_KeptAsIs()
    {
        var result = _resolver.ResolveDatabaseName("{unknown}", new Dictionary<string, object?>
        {
            ["Other"] = "value"
        });

        // {unknown} is not in parameters and not a built-in — should be kept
        result.Should().Contain("{unknown}");
    }

    [Fact]
    public void ResolveDatabaseName_CaseInsensitive()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["CompanyName"] = "Acme"
        };

        var result = _resolver.ResolveDatabaseName("{companyname}", parameters);
        result.Should().Be("Acme");
    }
}
