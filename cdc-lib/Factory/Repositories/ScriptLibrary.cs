using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Repositories;

/// <summary>
/// PostgreSQL-backed repository for individual scripts.
/// </summary>
public class ScriptLibrary : IScriptLibrary
{
    private readonly string _connectionString;
    private readonly ILogger<ScriptLibrary> _logger;

    public ScriptLibrary(string connectionString, ILogger<ScriptLibrary> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Script?> GetScriptAsync(Guid id)
    {
        const string sql = """
            SELECT id, name, description, type, content, file_path,
                   script_group_id, "order", created_at, updated_at
            FROM factory_scripts
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapScript(reader);
    }

    public async Task<IReadOnlyList<Script>> ListScriptsAsync(Guid? groupId = null)
    {
        var sql = """
            SELECT id, name, description, type, content, file_path,
                   script_group_id, "order", created_at, updated_at
            FROM factory_scripts
            """;

        if (groupId.HasValue)
            sql += " WHERE script_group_id = @groupId";

        sql += " ORDER BY \"order\"";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        if (groupId.HasValue)
            command.Parameters.AddWithValue("@groupId", groupId.Value);

        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<Script>();
        while (await reader.ReadAsync())
        {
            results.Add(MapScript(reader));
        }

        return results;
    }

    public async Task<Script> CreateScriptAsync(CreateScriptRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Content) && string.IsNullOrWhiteSpace(request.FilePath))
            throw new ArgumentException("Either Content or FilePath must be provided", nameof(request));

        const string insertSql = """
            INSERT INTO factory_scripts
                (name, description, type, content, file_path, script_group_id, "order")
            VALUES
                (@name, @description, @type, @content, @filePath, @scriptGroupId, @order)
            RETURNING id, name, description, type, content, file_path,
                      script_group_id, "order", created_at, updated_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(insertSql, connection);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", request.Type);
        command.Parameters.AddWithValue("@content", (object?)request.Content ?? DBNull.Value);
        command.Parameters.AddWithValue("@filePath", (object?)request.FilePath ?? DBNull.Value);
        command.Parameters.AddWithValue("@scriptGroupId", request.ScriptGroupId);
        command.Parameters.AddWithValue("@order", request.Order);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Failed to create script");

        var script = MapScript(reader);
        _logger.LogInformation("Created script '{Name}' (Id={Id})", script.Name, script.Id);
        return script;
    }

    public async Task<Script?> UpdateScriptAsync(Guid id, UpdateScriptRequest request)
    {
        const string updateSql = """
            UPDATE factory_scripts
            SET name = COALESCE(@name, name),
                description = COALESCE(@description, description),
                type = COALESCE(@type, type),
                content = COALESCE(@content, content),
                file_path = COALESCE(@filePath, file_path),
                "order" = COALESCE(@order, "order"),
                updated_at = NOW()
            WHERE id = @id
            RETURNING id, name, description, type, content, file_path,
                      script_group_id, "order", created_at, updated_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(updateSql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", (object?)request.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@type", (object?)request.Type ?? DBNull.Value);
        command.Parameters.AddWithValue("@content", (object?)request.Content ?? DBNull.Value);
        command.Parameters.AddWithValue("@filePath", (object?)request.FilePath ?? DBNull.Value);
        command.Parameters.AddWithValue("@order", (object?)request.Order ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapScript(reader);
    }

    public async Task<bool> DeleteScriptAsync(Guid id)
    {
        const string sql = "DELETE FROM factory_scripts WHERE id = @id";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    internal static Script MapScript(System.Data.IDataReader reader)
    {
        return new Script
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Content = reader.IsDBNull(reader.GetOrdinal("content")) ? null : reader.GetString(reader.GetOrdinal("content")),
            FilePath = reader.IsDBNull(reader.GetOrdinal("file_path")) ? null : reader.GetString(reader.GetOrdinal("file_path")),
            ScriptGroupId = reader.GetGuid(reader.GetOrdinal("script_group_id")),
            Order = reader.GetInt32(reader.GetOrdinal("order")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        };
    }
}
