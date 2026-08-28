using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Repositories;
using Xunit;

namespace cdc_api.Tests.Factory;

public class ConnectionRegistryTests
{
    private readonly ILogger<ConnectionRegistry> _logger = NullLogger<ConnectionRegistry>.Instance;

    [Fact]
    public void Constructor_ThrowsWhenConnectionStringIsNull()
    {
        var act = () => new ConnectionRegistry(null!, _logger);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("connectionString");
    }

    [Fact]
    public void Constructor_ThrowsWhenLoggerIsNull()
    {
        var act = () => new ConnectionRegistry("Host=localhost", null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("logger");
    }

    [Fact]
    public void MapConnection_MapsAllFieldsCorrectly()
    {
        var values = new object?[]
        {
            Guid.NewGuid(),
            "dev-sqlserver",
            "SqlServer",
            "sqlserver",
            1433,
            "Server=sqlserver;User Id=sa;Password=Test123!;TrustServerCertificate=true;",
            "Development SQL Server",
            true,
            DateTime.UtcNow,
            DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "platform", "host", "port", "connection_string", "description", "is_default", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var connection = ConnectionRegistry.MapConnection(reader);

        connection.Id.Should().Be((Guid)values[0]!);
        connection.Name.Should().Be("dev-sqlserver");
        connection.Platform.Should().Be("SqlServer");
        connection.Host.Should().Be("sqlserver");
        connection.Port.Should().Be(1433);
        connection.ConnectionString.Should().NotBeNullOrEmpty();
        connection.Description.Should().Be("Development SQL Server");
        connection.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void MapConnection_HandlesNullOptionalFields()
    {
        var values = new object?[]
        {
            Guid.NewGuid(),
            "qa-sqlserver",
            "SqlServer",
            DBNull.Value,
            DBNull.Value,
            "Server=qa;User Id=sa;Password=Test123!;",
            DBNull.Value,
            false,
            DateTime.UtcNow,
            DateTime.UtcNow
        };
        var schema = new[] { "id", "name", "platform", "host", "port", "connection_string", "description", "is_default", "created_at", "updated_at" };

        var reader = new FakeDataReader(values, schema);

        var connection = ConnectionRegistry.MapConnection(reader);

        connection.Host.Should().BeEmpty();
        connection.Port.Should().BeNull();
        connection.Description.Should().BeNull();
        connection.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenNameIsEmpty()
    {
        var registry = new ConnectionRegistry("Host=localhost", _logger);
        var request = new CreateConnectionRequest { Name = "", ConnectionString = "cs" };

        var act = () => registry.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
           .WithMessage("*Name is required*");
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenConnectionStringIsEmpty()
    {
        var registry = new ConnectionRegistry("Host=localhost", _logger);
        var request = new CreateConnectionRequest { Name = "test", ConnectionString = "" };

        var act = () => registry.CreateAsync(request);
        await act.Should().ThrowAsync<ArgumentException>()
           .WithMessage("*ConnectionString is required*");
    }
}

/// <summary>
/// Minimal fake <see cref="IDataReader"/> for testing MapConnection.
/// Returns a single row of pre-set values.
/// </summary>
internal class FakeDataReader : IDataReader
{
    private readonly object?[] _values;
    private readonly Dictionary<string, int> _ordinals = new();
    private bool _readConsumed;

    public FakeDataReader(object?[] values, string[] schema)
    {
        _values = values;
        for (int i = 0; i < schema.Length; i++)
            _ordinals[schema[i]] = i;
    }

    public bool Read() => !_readConsumed && (_readConsumed = true);
    public int GetOrdinal(string name) => _ordinals[name];
    public bool IsDBNull(int i) => _values[i] is null || _values[i] == DBNull.Value;
    public Guid GetGuid(int i) => (Guid)_values[i]!;
    public string GetString(int i) => (string)_values[i]!;
    public int GetInt32(int i) => (int)_values[i]!;
    public bool GetBoolean(int i) => (bool)_values[i]!;
    public DateTime GetDateTime(int i) => (DateTime)_values[i]!;
    public object GetValue(int i) => _values[i] ?? DBNull.Value;

    // — IDataReader members not used by MapConnection —
    public int FieldCount => _values.Length;
    public object this[int i] => _values[i]!;
    public object this[string name] => _values[_ordinals[name]]!;
    public int Depth => 0;
    public bool IsClosed => false;
    public int RecordsAffected => 0;
    public void Close() { }
    public bool NextResult() => false;
    public DataTable GetSchemaTable() => new();
    public Type GetFieldType(int i) => typeof(object);
    public string GetDataTypeName(int i) => typeof(object).Name;
    public string GetName(int i) => string.Empty;
    public int GetValues(object[] values) => 0;
    public byte GetByte(int i) => 0;
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public char GetChar(int i) => '\0';
    public long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public short GetInt16(int i) => 0;
    public long GetInt64(int i) => 0;
    public float GetFloat(int i) => 0f;
    public double GetDouble(int i) => 0d;
    public decimal GetDecimal(int i) => 0m;
    public IDataReader GetData(int i) => throw new NotImplementedException();
    public void Dispose() { }
}
