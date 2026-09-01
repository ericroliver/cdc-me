using System;
using System.Threading.Tasks;
using cdc_api.Controllers;
using CdcModels;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Softbase.Cdc;
using Xunit;

namespace cdc_api.Tests;

public class VersionControllerTests
{
    private readonly Mock<IVersionProvider> _versionProviderMock = new();
    private readonly VersionController _controller;

    public VersionControllerTests()
    {
        _controller = new VersionController(_versionProviderMock.Object);
    }

    [Fact]
    public void Get_ReturnsVersionInfo()
    {
        _versionProviderMock.SetupGet(v => v.Version).Returns("1.0.0");
        _versionProviderMock.SetupGet(v => v.InformationalVersion).Returns("1.0.0+sha.abc123");
        _versionProviderMock.SetupGet(v => v.CommitHash).Returns("abc123");
        _versionProviderMock.SetupGet(v => v.BuildDate).Returns("2026-08-28T00:00:00Z");
        _versionProviderMock.SetupGet(v => v.RuntimeVersion).Returns("10.0.0");

        var result = _controller.Get();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<VersionInfoDto>().Subject;
        dto.Version.Should().Be("1.0.0");
        dto.InformationalVersion.Should().Be("1.0.0+sha.abc123");
        dto.CommitHash.Should().Be("abc123");
        dto.BuildDate.Should().Be("2026-08-28T00:00:00Z");
        dto.RuntimeVersion.Should().Be("10.0.0");
    }

    [Fact]
    public void Constructor_ThrowsWhenVersionProviderIsNull()
    {
        var act = () => new VersionController(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

public class VersionProviderTests
{
    [Fact]
    public void VersionProvider_ReturnsNonEmptyValues()
    {
        // Set env vars to test parsing
        Environment.SetEnvironmentVariable("GIT_COMMIT", "test1234");
        Environment.SetEnvironmentVariable("BUILD_DATE", "2026-08-28T01:00:00Z");

        var provider = new VersionProvider();

        provider.Version.Should().NotBeEmpty();
        provider.InformationalVersion.Should().NotBeEmpty();
        provider.CommitHash.Should().Be("test1234");
        provider.BuildDate.Should().Be("2026-08-28T01:00:00Z");
        provider.RuntimeVersion.Should().NotBeEmpty();

        // Cleanup
        Environment.SetEnvironmentVariable("GIT_COMMIT", null);
        Environment.SetEnvironmentVariable("BUILD_DATE", null);
    }

    [Fact]
    public void VersionProvider_ReturnsUnknown_WhenEnvVarsNotSet()
    {
        Environment.SetEnvironmentVariable("GIT_COMMIT", null);
        Environment.SetEnvironmentVariable("BUILD_DATE", null);

        var provider = new VersionProvider();

        provider.CommitHash.Should().NotBeEmpty();
        provider.BuildDate.Should().NotBeEmpty();
    }

    [Fact]
    public void VersionProvider_RuntimeVersion_IsValid()
    {
        var provider = new VersionProvider();
        // Runtime version should be a valid version string
        Version.TryParse(provider.RuntimeVersion, out _).Should().BeTrue();
    }
}
