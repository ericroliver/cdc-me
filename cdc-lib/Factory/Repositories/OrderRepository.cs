using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Repositories;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IOrderRepository"/>.
/// Handles all persistence for factory orders, their parameters, script group
/// associations, and provisioned database records.
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly string _connectionString;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(string connectionString, ILogger<OrderRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order> CreateAsync(OrderRequest request)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TemplateId = request.TemplateId,
            TargetConnectionId = request.TargetConnectionId,
            TargetDatabaseName = request.TargetDatabaseName,
            Status = nameof(OrderStatus.Pending),
            CreatedAt = DateTime.UtcNow
        };

        const string sql = """
            INSERT INTO factory_orders
                (id, template_id, target_connection_id, target_database_name, status, created_at)
            VALUES
                (@id, @templateId, @targetConnectionId, @targetDatabaseName, @status, @createdAt)
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", order.Id);
        command.Parameters.AddWithValue("@templateId", order.TemplateId);
        command.Parameters.AddWithValue("@targetConnectionId", (object?)order.TargetConnectionId ?? DBNull.Value);
        command.Parameters.AddWithValue("@targetDatabaseName", order.TargetDatabaseName);
        command.Parameters.AddWithValue("@status", order.Status);
        command.Parameters.AddWithValue("@createdAt", order.CreatedAt);

        await command.ExecuteNonQueryAsync();

        _logger.LogInformation("Created order {OrderId} for template {TemplateId}", order.Id, order.TemplateId);
        return order;
    }

    public async Task UpdateStatusAsync(
        Guid orderId, OrderStatus status,
        DateTime? startedAt = null, DateTime? completedAt = null)
    {
        var sql = "UPDATE factory_orders SET status = @status";
        if (startedAt.HasValue)
            sql += ", started_at = @startedAt";
        if (completedAt.HasValue)
            sql += ", completed_at = @completedAt";
        sql += " WHERE id = @id";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", orderId);
        command.Parameters.AddWithValue("@status", status.ToString());
        if (startedAt.HasValue)
            command.Parameters.AddWithValue("@startedAt", startedAt.Value);
        if (completedAt.HasValue)
            command.Parameters.AddWithValue("@completedAt", completedAt.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task FailAsync(Guid orderId, string errorMessage)
    {
        const string sql = """
            UPDATE factory_orders
            SET status = @status,
                error_message = @errorMessage,
                completed_at = COALESCE(completed_at, NOW())
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", orderId);
        command.Parameters.AddWithValue("@status", nameof(OrderStatus.Failed));
        command.Parameters.AddWithValue("@errorMessage", errorMessage);

        await command.ExecuteNonQueryAsync();
    }

    public async Task PersistOrderDetailsAsync(
        Guid orderId,
        IReadOnlyList<Guid> scriptGroupIds,
        IReadOnlyDictionary<string, object?> parameters)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var groupId in scriptGroupIds)
            {
                const string groupSql = """
                    INSERT INTO factory_order_script_groups (order_id, script_group_id)
                    VALUES (@orderId, @scriptGroupId)
                    ON CONFLICT DO NOTHING
                    """;
                await using var groupCmd = new NpgsqlCommand(groupSql, connection, transaction);
                groupCmd.Parameters.AddWithValue("@orderId", orderId);
                groupCmd.Parameters.AddWithValue("@scriptGroupId", groupId);
                await groupCmd.ExecuteNonQueryAsync();
            }

            foreach (var (key, value) in parameters)
            {
                const string paramSql = """
                    INSERT INTO factory_order_parameters (order_id, key, value)
                    VALUES (@orderId, @key, @value)
                    ON CONFLICT (order_id, key) DO UPDATE SET value = @value
                    """;
                await using var paramCmd = new NpgsqlCommand(paramSql, connection, transaction);
                paramCmd.Parameters.AddWithValue("@orderId", orderId);
                paramCmd.Parameters.AddWithValue("@key", key);
                paramCmd.Parameters.AddWithValue("@value", (object?)value?.ToString() ?? DBNull.Value);
                await paramCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task RecordProvisionedDatabaseAsync(
        Guid orderId, string databaseName, Guid connectionId, Guid templateId)
    {
        const string sql = """
            INSERT INTO factory_provisioned_databases
                (order_id, database_name, connection_id, template_id, status)
            VALUES
                (@orderId, @databaseName, @connectionId, @templateId, @status)
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@databaseName", databaseName);
        command.Parameters.AddWithValue("@connectionId", connectionId);
        command.Parameters.AddWithValue("@templateId", templateId);
        command.Parameters.AddWithValue("@status", "Active");

        await command.ExecuteNonQueryAsync();

        _logger.LogInformation(
            "Recorded provisioned database '{DbName}' (order={OrderId}, connection={ConnectionId})",
            databaseName, orderId, connectionId);
    }

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        const string sql = """
            SELECT id, template_id, target_connection_id, target_database_name,
                   status, error_message, created_at, started_at, completed_at
            FROM factory_orders
            WHERE id = @id
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return MapOrder(reader);
    }

    public async Task<IReadOnlyList<Order>> ListAsync()
    {
        const string sql = """
            SELECT id, template_id, target_connection_id, target_database_name,
                   status, error_message, created_at, started_at, completed_at
            FROM factory_orders
            ORDER BY created_at DESC
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<Order>();
        while (await reader.ReadAsync())
        {
            results.Add(MapOrder(reader));
        }

        return results;
    }

    internal static Order MapOrder(System.Data.IDataReader reader)
    {
        return new Order
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            TemplateId = reader.GetGuid(reader.GetOrdinal("template_id")),
            TargetConnectionId = reader.IsDBNull(reader.GetOrdinal("target_connection_id"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("target_connection_id")),
            TargetDatabaseName = reader.GetString(reader.GetOrdinal("target_database_name")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message"))
                ? null
                : reader.GetString(reader.GetOrdinal("error_message")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            StartedAt = reader.IsDBNull(reader.GetOrdinal("started_at"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("started_at")),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("completed_at"))
        };
    }
}
