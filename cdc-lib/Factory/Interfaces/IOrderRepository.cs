using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Persistence layer for factory orders — order records, status transitions,
/// parameters, and script group associations.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Creates a new order record with status Pending.
    /// </summary>
    Task<Order> CreateAsync(OrderRequest request);

    /// <summary>
    /// Updates the status of an order, optionally setting started/completed timestamps.
    /// </summary>
    Task UpdateStatusAsync(Guid orderId, OrderStatus status, DateTime? startedAt = null, DateTime? completedAt = null);

    /// <summary>
    /// Marks an order as Failed with an error message.
    /// </summary>
    Task FailAsync(Guid orderId, string errorMessage);

    /// <summary>
    /// Persists script group associations and parameters for an order.
    /// </summary>
    Task PersistOrderDetailsAsync(Guid orderId, IReadOnlyList<Guid> scriptGroupIds, IReadOnlyDictionary<string, object?> parameters);

    /// <summary>
    /// Records a provisioned database in the registry table.
    /// </summary>
    Task RecordProvisionedDatabaseAsync(Guid orderId, string databaseName, Guid connectionId, Guid templateId);

    /// <summary>
    /// Retrieves an order by ID.
    /// </summary>
    Task<Order?> GetByIdAsync(Guid id);

    /// <summary>
    /// Lists all orders.
    /// </summary>
    Task<IReadOnlyList<Order>> ListAsync();

    /// <summary>
    /// Deletes an order by ID, including its parameters and script group associations.
    /// </summary>
    Task<bool> DeleteAsync(Guid id);
}
