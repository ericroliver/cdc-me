using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Repositories.SqlServer;

/// <summary>
/// SQL Server-backed implementation of <see cref="IOrderRepository"/>.
/// Handles all persistence for factory orders, their parameters, script group
/// associations, and provisioned database records.
/// </summary>
public class SqlServerOrderRepository : IOrderRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerOrderRepository> _logger;

    public SqlServerOrderRepository(string connectionString, ILogger<SqlServerOrderRepository> logger)
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
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
                completed_at = COALESCE(completed_at, SYSUTCDATETIME())
            WHERE id = @id
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
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
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            foreach (var groupId in scriptGroupIds)
            {
                const string groupSql = """
                    IF NOT EXISTS (
                        SELECT 1 FROM factory_order_script_groups
                        WHERE order_id = @orderId AND script_group_id = @scriptGroupId
                    )
                    INSERT INTO factory_order_script_groups (order_id, script_group_id)
                    VALUES (@orderId, @scriptGroupId)
                    """;
                await using var groupCmd = new SqlCommand(groupSql, connection, transaction);
                groupCmd.Parameters.AddWithValue("@orderId", orderId);
                groupCmd.Parameters.AddWithValue("@scriptGroupId", groupId);
                await groupCmd.ExecuteNonQueryAsync();
            }

            foreach (var (key, value) in parameters)
            {
                const string paramSql = """
                    MERGE factory_order_parameters AS target
                    USING (SELECT @orderId AS order_id, @key AS [key], @value AS value) AS source
                    ON (target.order_id = source.order_id AND target.[key] = source.[key])
                    WHEN MATCHED THEN
                        UPDATE SET value = source.value
                    WHEN NOT MATCHED THEN
                        INSERT (order_id, [key], value) VALUES (source.order_id, source.[key], source.value);
                    """;
                await using var paramCmd = new SqlCommand(paramSql, connection, transaction);
                paramCmd.Parameters.AddWithValue("@orderId", orderId);
                paramCmd.Parameters.AddWithValue("@key", key);
                paramCmd.Parameters.AddWithValue("@value", (object?)value?.ToString() ?? (object)DBNull.Value);
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var order = MapOrder(reader);
        await reader.CloseAsync();
        await LoadScriptGroupIdsAsync(connection, order);
        return order;
    }

    public async Task<IReadOnlyList<Order>> ListAsync()
    {
        const string sql = """
            SELECT id, template_id, target_connection_id, target_database_name,
                   status, error_message, created_at, started_at, completed_at
            FROM factory_orders
            ORDER BY created_at DESC
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var results = new List<Order>();
        while (await reader.ReadAsync())
        {
            results.Add(MapOrder(reader));
        }

        await reader.CloseAsync();
        await LoadScriptGroupIdsBatchAsync(connection, results);
        return results;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        const string scriptGroupsSql = "DELETE FROM factory_order_script_groups WHERE order_id = @id";
        const string parametersSql = "DELETE FROM factory_order_parameters WHERE order_id = @id";
        const string provisionedDbsSql = "DELETE FROM factory_provisioned_databases WHERE order_id = @id";
        const string orderSql = "DELETE FROM factory_orders WHERE id = @id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        try
        {
            await using (var cmd = new SqlCommand(scriptGroupsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new SqlCommand(parametersSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new SqlCommand(provisionedDbsSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new SqlCommand(orderSql, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@id", id);
                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Deleted order {OrderId}", id);
                }

                return rowsAffected > 0;
            }
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task LoadScriptGroupIdsAsync(SqlConnection connection, Order order)
    {
        const string groupSql = """
            SELECT script_group_id
            FROM factory_order_script_groups
            WHERE order_id = @orderId
            ORDER BY script_group_id
            """;

        await using var groupCmd = new SqlCommand(groupSql, connection);
        groupCmd.Parameters.AddWithValue("@orderId", order.Id);

        var groupIds = new List<Guid>();
        await using var groupReader = await groupCmd.ExecuteReaderAsync();
        while (await groupReader.ReadAsync())
        {
            groupIds.Add(groupReader.GetGuid(0));
        }

        order.ScriptGroupIds = groupIds;
    }

    private async Task LoadScriptGroupIdsBatchAsync(SqlConnection connection, List<Order> orders)
    {
        if (orders.Count == 0)
            return;

        var orderIds = orders.Select(o => o.Id).ToHashSet();

        // Build parameterized IN clause — one parameter per ID
        var paramNames = orderIds.Select((_, i) => $"@id{i}").ToList();
        var groupSql = $"""
            SELECT order_id, script_group_id
            FROM factory_order_script_groups
            WHERE order_id IN ({string.Join(", ", paramNames)})
            ORDER BY script_group_id
            """;

        var groupMap = new Dictionary<Guid, List<Guid>>();
        await using var groupCmd = new SqlCommand(groupSql, connection);
        var idx = 0;
        foreach (var orderId in orderIds)
        {
            groupCmd.Parameters.AddWithValue(paramNames[idx], orderId);
            idx++;
        }

        await using var groupReader = await groupCmd.ExecuteReaderAsync();
        while (await groupReader.ReadAsync())
        {
            var orderId = groupReader.GetGuid(0);
            var scriptGroupId = groupReader.GetGuid(1);
            if (!groupMap.TryGetValue(orderId, out var list))
            {
                list = new List<Guid>();
                groupMap[orderId] = list;
            }
            list.Add(scriptGroupId);
        }

        foreach (var order in orders)
        {
            order.ScriptGroupIds = groupMap.TryGetValue(order.Id, out var ids)
                ? ids
                : Array.Empty<Guid>();
        }
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
