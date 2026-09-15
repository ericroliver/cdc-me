using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories.SqlServer;
using Xunit;

namespace cdc_api.Tests.Factory;

public class SqlServerDatabaseTemplateRepositoryTests
{
    private readonly ILogger<SqlServerDatabaseTemplateRepository> _logger = NullLogger<SqlServerDatabaseTemplateRepository>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var storageProvider = new Mock<ITemplateStorageProvider>().Object;
        var act = () => new SqlServerDatabaseTemplateRepository(null!, storageProvider, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenStorageProviderIsNull()
    {
        var act = () => new SqlServerDatabaseTemplateRepository("Server=localhost", null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("storageProvider");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var storageProvider = new Mock<ITemplateStorageProvider>().Object;
        var act = () => new SqlServerDatabaseTemplateRepository("Server=localhost", storageProvider, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapTemplate_MapsAllFieldsCorrectly()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var values = new object?[]
        {
            id,
            "Northwind.bak",
            "1.0",
            "SqlServer",
            "/templates/northwind.bak",
            "Northwind template",
            "abc123",
            now,
            "admin"
        };
        var schema = new[] { "id", "name", "version", "platform", "file_path", "description", "checksum", "created_at", "created_by" };

        var reader = new FakeDataReader(values, schema);

        var template = SqlServerDatabaseTemplateRepository.MapTemplate(reader);

        template.Id.Should().Be(id);
        template.Name.Should().Be("Northwind.bak");
        template.Version.Should().Be("1.0");
        template.Platform.Should().Be("SqlServer");
        template.FilePath.Should().Be("/templates/northwind.bak");
        template.Description.Should().Be("Northwind template");
        template.Checksum.Should().Be("abc123");
        template.CreatedBy.Should().Be("admin");
    }

    [Fact]
    public void MapTemplate_HandlesNullOptionalFields()
    {
        var values = new object?[]
        {
            Guid.NewGuid(),
            "Empty.bak",
            "1.0",
            "SqlServer",
            "/templates/empty.bak",
            DBNull.Value,
            DBNull.Value,
            DateTime.UtcNow,
            DBNull.Value
        };
        var schema = new[] { "id", "name", "version", "platform", "file_path", "description", "checksum", "created_at", "created_by" };

        var reader = new FakeDataReader(values, schema);

        var template = SqlServerDatabaseTemplateRepository.MapTemplate(reader);

        template.Description.Should().BeNull();
        template.Checksum.Should().BeNull();
        template.CreatedBy.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_ThrowsWhenNameIsEmpty()
    {
        var storageProvider = new Mock<ITemplateStorageProvider>().Object;
        var repo = new SqlServerDatabaseTemplateRepository("Server=localhost", storageProvider, _logger);
        var request = new RegisterTemplateRequest { Name = "", FilePath = "/templates/test.bak", Version = "1.0" };

        var act = () => repo.RegisterAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
           .WithMessage("*Name is required*");
    }

    [Fact]
    public async Task RegisterAsync_ThrowsWhenFilePathIsEmpty()
    {
        var storageProvider = new Mock<ITemplateStorageProvider>().Object;
        var repo = new SqlServerDatabaseTemplateRepository("Server=localhost", storageProvider, _logger);
        var request = new RegisterTemplateRequest { Name = "test", FilePath = "", Version = "1.0" };

        var act = () => repo.RegisterAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
           .WithMessage("*FilePath is required*");
    }
}
