using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Engine;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IConnectionRegistry"/>.
/// Manages default-connection enforcement: setting IsDefault on one
/// connection clears it on the previous default within the same transaction.
/// </summary>
public class ConnectionRegistry : IConnectionRegistry
{
    private readonly string _connectionString;
    private readonly ILogger<ConnectionRegistry> _logger;

    public ConnectionRegistry(string connectionString, ILogger<ConnectionRegistry> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Connection?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT id, name, platform, host, port, connection_string,
                   description, is_default, created_at, updated_at
            FROM factory_connections
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapConnection(reader);
    }

    public async Task<Connection?> GetDefaultAsync()
    {
        const string sql = """
            SELECT id, name, platform, host, port, connection_string,
                   description, is_default, created_at, updated_at
            FROM factory_connections
            WHERE is_default = TRUE
            LIMIT 1
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapConnection(reader);
    }

    public async Task<IReadOnlyList<Connection>> ListAsync()
    {
        const string sql = """
            SELECT id, name, platform, host, port, connection_string,
                   description, is_default, created_at, updated_at
            FROM factory_connections
            ORDER BY created_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<Connection>();
        while (await reader.ReadAsync())
        {
            results.Add(MapConnection(reader));
        }

        return results;
    }

    public async Task<Connection> CreateAsync(CreateConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ConnectionString))
            throw new ArgumentException("ConnectionString is required", nameof(request));

        const string insertSql = """
            INSERT INTO factory_connections
                (name, platform, host, port, connection_string, description, is_default)
            VALUES
                (@name, @platform, @host, @port, @connectionString, @description, @isDefault)
            RETURNING id, name, platform, host, port, connection_string,
                      description, is_default, created_at, updated_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Clear existing default if this new connection is being set as default
            if (request.IsDefault)
            {
                await ClearExistingDefaultAsync(connection, transaction);
            }

            await using var command = new NpgsqlCommand(insertSql, connection, transaction);
            command.Parameters.AddWithValue("@name", request.Name);
            command.Parameters.AddWithValue("@platform", request.Platform);
            command.Parameters.AddWithValue("@host", (object?)request.Host ?? DBNull.Value);
            command.Parameters.AddWithValue("@port", (object?)request.Port ?? DBNull.Value);
            command.Parameters.AddWithValue("@connectionString", request.ConnectionString);
            command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@isDefault", request.IsDefault);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("Failed to create connection");

            var created = MapConnection(reader);
            await reader.CloseAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Created connection '{Name}' (Id={Id})", created.Name, created.Id);
            return created;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create connection '{Name}'", request.Name);
            throw;
        }
    }

    public async Task<Connection?> UpdateAsync(Guid id, UpdateConnectionRequest request)
    {
        const string updateSql = """
            UPDATE factory_connections
            SET host = COALESCE(@host, host),
                port = COALESCE(@port, port),
                connection_string = COALESCE(@connectionString, connection_string),
                description = COALESCE(@description, description),
                is_default = COALESCE(@isDefault, is_default),
                updated_at = NOW()
            WHERE id = @id
            RETURNING id, name, platform, host, port, connection_string,
                      description, is_default, created_at, updated_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Clear existing default if this connection is being set as default
            if (request.IsDefault == true)
            {
                await ClearExistingDefaultAsync(connection, transaction, id);
            }

            await using var command = new NpgsqlCommand(updateSql, connection, transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@host", (object?)request.Host ?? DBNull.Value);
            command.Parameters.AddWithValue("@port", (object?)request.Port ?? DBNull.Value);
            command.Parameters.AddWithValue("@connectionString", (object?)request.ConnectionString ?? DBNull.Value);
            command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@isDefault", (object?)request.IsDefault ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await reader.CloseAsync();
                await transaction.RollbackAsync();
                return null;
            }

            var updated = MapConnection(reader);
            await reader.CloseAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Updated connection (Id={Id})", id);
            return updated;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to update connection (Id={Id})", id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        const string sql = "DELETE FROM factory_connections WHERE id = @id";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        try
        {
            var rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected > 0)
            {
                _logger.LogInformation("Deleted connection (Id={Id})", id);
            }

            return rowsAffected > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            _logger.LogWarning("Cannot delete connection {Id}: referenced by existing orders", id);
            throw new ReferencedByOrdersException(
                "connection",
                "Cannot delete connection referenced by existing orders.",
                ex);
        }
    }

    public async Task<bool> TestConnectionAsync(Guid id)
    {
        var connection = await GetByIdAsync(id);
        if (connection is null)
            return false;

        try
        {
            using var targetConnection = new NpgsqlConnection(connection.ConnectionString);
            await targetConnection.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection test failed for '{Name}' (Id={Id})", connection.Name, connection.Id);
            return false;
        }
    }

    /// <summary>
    /// Clears the is_default flag on all connections except the one being set as default.
    /// </summary>
    private static async Task ClearExistingDefaultAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid? excludeId = null)
    {
        if (excludeId.HasValue)
        {
            const string sql = """
                UPDATE factory_connections
                SET is_default = FALSE, updated_at = NOW()
                WHERE is_default = TRUE AND id != @excludeId
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@excludeId", excludeId.Value);
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            const string sql = """
                UPDATE factory_connections
                SET is_default = FALSE, updated_at = NOW()
                WHERE is_default = TRUE
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Maps a data reader row to a <see cref="Connection"/> object.
    /// Exposed internally for testing.
    /// </summary>
    internal static Connection MapConnection(IDataReader reader)
    {
        return new Connection
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Platform = reader.GetString(reader.GetOrdinal("platform")),
            Host = reader.IsDBNull(reader.GetOrdinal("host")) ? string.Empty : reader.GetString(reader.GetOrdinal("host")),
            Port = reader.IsDBNull(reader.GetOrdinal("port")) ? null : reader.GetInt32(reader.GetOrdinal("port")),
            ConnectionString = reader.GetString(reader.GetOrdinal("connection_string")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            IsDefault = reader.GetBoolean(reader.GetOrdinal("is_default")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        };
    }
}
