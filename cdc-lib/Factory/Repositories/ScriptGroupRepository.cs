using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Repositories;

/// <summary>
/// PostgreSQL-backed repository for script groups.
/// Manages both factory_script_groups and factory_script_group_dependencies tables.
/// Dependencies are managed as a full-replace on create/update.
/// </summary>
public class ScriptGroupRepository : IScriptGroupRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ScriptGroupRepository> _logger;

    public ScriptGroupRepository(string connectionString, ILogger<ScriptGroupRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScriptGroup?> GetGroupAsync(Guid id)
    {
        const string sql = """
            SELECT id, name, description, layer, "order", created_at, updated_at
            FROM factory_script_groups
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var group = MapGroup(reader);
        await reader.CloseAsync();

        // Load dependencies
        group.Dependencies = await LoadDependenciesAsync(connection, id);

        return group;
    }

    public async Task<IReadOnlyList<ScriptGroup>> ListGroupsAsync(int? layer = null)
    {
        var sql = """
            SELECT id, name, description, layer, "order", created_at, updated_at
            FROM factory_script_groups
            """;

        if (layer.HasValue)
            sql += " WHERE layer = @layer";

        sql += " ORDER BY layer, \"order\"";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        if (layer.HasValue)
            command.Parameters.AddWithValue("@layer", layer.Value);

        await using var reader = await command.ExecuteReaderAsync();
        var groups = new List<ScriptGroup>();
        while (await reader.ReadAsync())
        {
            groups.Add(MapGroup(reader));
        }
        await reader.CloseAsync();

        // Load dependencies for each group
        foreach (var group in groups)
        {
            group.Dependencies = await LoadDependenciesAsync(connection, group.Id);
        }

        return groups;
    }

    public async Task<ScriptGroup> CreateGroupAsync(CreateScriptGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request));

        const string insertSql = """
            INSERT INTO factory_script_groups
                (name, description, layer, "order")
            VALUES
                (@name, @description, @layer, @order)
            RETURNING id, name, description, layer, "order", created_at, updated_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using var command = new NpgsqlCommand(insertSql, connection, transaction);
            command.Parameters.AddWithValue("@name", request.Name);
            command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@layer", request.Layer);
            command.Parameters.AddWithValue("@order", request.Order);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("Failed to create script group");

            var group = MapGroup(reader);
            await reader.CloseAsync();

            // Insert dependencies
            await SaveDependenciesAsync(connection, transaction, group.Id, request.Dependencies);

            await transaction.CommitAsync();

            group.Dependencies = request.Dependencies;
            _logger.LogInformation("Created script group '{Name}' (Id={Id})", group.Name, group.Id);
            return group;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create script group '{Name}'", request.Name);
            throw;
        }
    }

    public async Task<ScriptGroup?> UpdateGroupAsync(Guid id, UpdateScriptGroupRequest request)
    {
        const string updateSql = """
            UPDATE factory_script_groups
            SET name = COALESCE(@name, name),
                description = COALESCE(@description, description),
                layer = COALESCE(@layer, layer),
                "order" = COALESCE(@order, "order"),
                updated_at = NOW()
            WHERE id = @id
            RETURNING id, name, description, layer, "order", created_at, updated_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using var command = new NpgsqlCommand(updateSql, connection, transaction);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@name", (object?)request.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@layer", (object?)request.Layer ?? DBNull.Value);
            command.Parameters.AddWithValue("@order", (object?)request.Order ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                await reader.CloseAsync();
                await transaction.RollbackAsync();
                return null;
            }

            var group = MapGroup(reader);
            await reader.CloseAsync();

            if (request.Dependencies is not null)
            {
                await SaveDependenciesAsync(connection, transaction, id, request.Dependencies);
                group.Dependencies = request.Dependencies;
            }
            else
            {
                group.Dependencies = await LoadDependenciesAsync(connection, transaction, id);
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Updated script group (Id={Id})", id);
            return group;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to update script group (Id={Id})", id);
            throw;
        }
    }

    public async Task<bool> DeleteGroupAsync(Guid id)
    {
        const string sql = "DELETE FROM factory_script_groups WHERE id = @id";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected > 0)
            _logger.LogInformation("Deleted script group (Id={Id})", id);

        return rowsAffected > 0;
    }

    private async Task<IReadOnlyList<Guid>> LoadDependenciesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid groupId)
    {
        const string sql = """
            SELECT depends_on_id
            FROM factory_script_group_dependencies
            WHERE group_id = @groupId
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@groupId", groupId);

        await using var reader = await command.ExecuteReaderAsync();
        var deps = new List<Guid>();
        while (await reader.ReadAsync())
        {
            deps.Add(reader.GetGuid(0));
        }

        return deps;
    }

    private async Task<IReadOnlyList<Guid>> LoadDependenciesAsync(
        NpgsqlConnection connection,
        Guid groupId)
    {
        return await LoadDependenciesAsync(connection, null, groupId);
    }

    private static async Task SaveDependenciesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid groupId,
        IReadOnlyList<Guid> dependencies)
    {
        // Clear existing dependencies
        const string deleteSql = "DELETE FROM factory_script_group_dependencies WHERE group_id = @groupId";
        await using var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction);
        deleteCommand.Parameters.AddWithValue("@groupId", groupId);
        await deleteCommand.ExecuteNonQueryAsync();

        // Insert new dependencies
        foreach (var depId in dependencies)
        {
            const string insertSql = """
                INSERT INTO factory_script_group_dependencies (group_id, depends_on_id)
                VALUES (@groupId, @dependsOnId)
                """;
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("@groupId", groupId);
            insertCommand.Parameters.AddWithValue("@dependsOnId", depId);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    internal static ScriptGroup MapGroup(System.Data.IDataReader reader)
    {
        return new ScriptGroup
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            Layer = reader.GetInt32(reader.GetOrdinal("layer")),
            Order = reader.GetInt32(reader.GetOrdinal("order")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        };
    }
}
