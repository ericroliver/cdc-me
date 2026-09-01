using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Request to provision a new database through the factory engine.
/// </summary>
public class OrderRequest
{
    public Guid TemplateId { get; set; }
    public Guid? TargetConnectionId { get; set; }
    public string? TargetConnectionName { get; set; }
    public string TargetDatabaseName { get; set; } = string.Empty;
    public IReadOnlyList<Guid> ScriptGroupIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyDictionary<string, object?>? Parameters { get; set; }
    public string? ParameterFilePath { get; set; }
}

/// <summary>
/// The factory engine — orchestrates the full provisioning workflow:
/// resolve connection → validate → resolve parameters → restore → hydrate → deliver.
/// </summary>
public interface IDatabaseFactory
{
    /// <summary>
    /// Processes an order through the full factory pipeline.
    /// Returns the completed order with final status.
    /// </summary>
    Task<Order> OrderAsync(OrderRequest request);
}
