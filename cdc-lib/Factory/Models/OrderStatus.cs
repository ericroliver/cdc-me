namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// Lifecycle states for a factory order.
/// </summary>
public enum OrderStatus
{
    Pending,
    Resolving,
    Validating,
    Restoring,
    Hydrating,
    Delivered,
    Failed
}
