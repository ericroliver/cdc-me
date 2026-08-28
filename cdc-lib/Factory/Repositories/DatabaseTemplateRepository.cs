using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;
using Softbase.Cdc.Factory.Providers;

namespace Softbase.Cdc.Factory.Repositories;

/// <summary>
/// PostgreSQL-backed repository for template metadata.
/// Files are stored via <see cref="ITemplateStorageProvider"/>; this manages the DB records.
/// </summary>
public class DatabaseTemplateRepository : IDatabaseTemplateRepository
{
    private readonly string _connectionString;
    private readonly ITemplateStorageProvider _storageProvider;
    private readonly ILogger<DatabaseTemplateRepository> _logger;

    public DatabaseTemplateRepository(
        string connectionString,
        ITemplateStorageProvider storageProvider,
        ILogger<DatabaseTemplateRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Template?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT id, name, version, platform, file_path,
                   description, checksum, created_at, created_by
            FROM factory_templates
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapTemplate(reader);
    }

    public async Task<IReadOnlyList<Template>> ListAsync()
    {
        const string sql = """
            SELECT id, name, version, platform, file_path,
                   description, checksum, created_at, created_by
            FROM factory_templates
            ORDER BY created_at
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<Template>();
        while (await reader.ReadAsync())
        {
            results.Add(MapTemplate(reader));
        }

        return results;
    }

    public async Task<Template> RegisterAsync(RegisterTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required", nameof(request));
        if (string.IsNullOrWhiteSpace(request.FilePath))
            throw new ArgumentException("FilePath is required", nameof(request));

        if (!_storageProvider.Exists(request.FilePath))
            throw new FileNotFoundException($"Template file not found: {request.FilePath}");

        // Compute checksum if not provided
        var checksum = request.Checksum ?? LocalFileStorageProvider.ComputeChecksum(
            System.IO.Path.IsPathRooted(request.FilePath)
                ? request.FilePath
                : request.FilePath);

        const string insertSql = """
            INSERT INTO factory_templates
                (name, version, platform, file_path, description, checksum, created_by)
            VALUES
                (@name, @version, @platform, @filePath, @description, @checksum, @createdBy)
            RETURNING id, name, version, platform, file_path,
                      description, checksum, created_at, created_by
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(insertSql, connection);
        command.Parameters.AddWithValue("@name", request.Name);
        command.Parameters.AddWithValue("@version", request.Version);
        command.Parameters.AddWithValue("@platform", request.Platform);
        command.Parameters.AddWithValue("@filePath", request.FilePath);
        command.Parameters.AddWithValue("@description", (object?)request.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@checksum", (object?)checksum ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdBy", (object?)request.CreatedBy ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Failed to register template");

        var template = MapTemplate(reader);
        _logger.LogInformation("Registered template '{Name}' (Id={Id})", template.Name, template.Id);
        return template;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        // Get the template to find its file path
        var template = await GetByIdAsync(id);
        if (template is null)
            return false;

        // Delete the file from storage
        await _storageProvider.DeleteAsync(template.FilePath);

        // Delete the metadata record
        const string sql = "DELETE FROM factory_templates WHERE id = @id";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
        _logger.LogInformation("Deleted template '{Name}' (Id={Id})", template.Name, template.Id);
        return true;
    }

    public async Task<bool> VerifyAsync(Guid id)
    {
        var template = await GetByIdAsync(id);
        if (template is null)
            return false;

        if (!_storageProvider.Exists(template.FilePath))
        {
            _logger.LogWarning("Template file not found for '{Name}' (Id={Id})", template.Name, template.Id);
            return false;
        }

        // Verify checksum if one is stored
        if (!string.IsNullOrEmpty(template.Checksum))
        {
            var actualChecksum = LocalFileStorageProvider.ComputeChecksum(template.FilePath);
            if (!string.Equals(actualChecksum, template.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Checksum mismatch for template '{Name}': expected={Expected}, actual={Actual}",
                    template.Name, template.Checksum, actualChecksum);
                return false;
            }
        }

        return true;
    }

    internal static Template MapTemplate(System.Data.IDataReader reader)
    {
        return new Template
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Version = reader.GetString(reader.GetOrdinal("version")),
            Platform = reader.GetString(reader.GetOrdinal("platform")),
            FilePath = reader.GetString(reader.GetOrdinal("file_path")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by"))
        };
    }
}
