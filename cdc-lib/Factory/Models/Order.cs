using System;
using System.Collections.Generic;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// A factory order — a request to provision a database from a template,
/// hydrate it with scripts, and register the result.
/// </summary>
public class Order
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public Guid? TargetConnectionId { get; set; }
    public string TargetDatabaseName { get; set; } = string.Empty;
    public string Status { get; set; } = nameof(OrderStatus.Pending);
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The script group IDs associated with this order.
    /// </summary>
    public IReadOnlyList<Guid> ScriptGroupIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    /// Merged parameters for this order (inline + file).
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>();
}
