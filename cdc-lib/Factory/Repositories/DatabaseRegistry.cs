using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IDatabaseRegistry"/>.
/// Read-only in Phase 1; the factory engine creates entries.
/// </summary>
public class DatabaseRegistry : IDatabaseRegistry
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseRegistry> _logger;

    public DatabaseRegistry(string connectionString, ILogger<DatabaseRegistry> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ProvisionedDatabase>> ListAsync()
    {
        const string sql = """
            SELECT id, order_id, database_name, connection_id,
                   template_id, status, created_at, decommissioned_at
            FROM factory_provisioned_databases
            ORDER BY created_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<ProvisionedDatabase>();
        while (await reader.ReadAsync())
        {
            results.Add(MapProvisionedDatabase(reader));
        }

        return results;
    }

    public async Task<ProvisionedDatabase?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT id, order_id, database_name, connection_id,
                   template_id, status, created_at, decommissioned_at
            FROM factory_provisioned_databases
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapProvisionedDatabase(reader);
    }

    internal static ProvisionedDatabase MapProvisionedDatabase(System.Data.IDataReader reader)
    {
        return new ProvisionedDatabase
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            OrderId = reader.GetGuid(reader.GetOrdinal("order_id")),
            DatabaseName = reader.GetString(reader.GetOrdinal("database_name")),
            ConnectionId = reader.GetGuid(reader.GetOrdinal("connection_id")),
            TemplateId = reader.GetGuid(reader.GetOrdinal("template_id")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            DecommissionedAt = reader.IsDBNull(reader.GetOrdinal("decommissioned_at"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("decommissioned_at"))
        };
    }
}
