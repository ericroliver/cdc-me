using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Central registry for named database server connections.
/// Everything else (templates, orders, provisioned databases) references
/// connections by ID rather than embedding raw connection strings.
/// </summary>
public interface IConnectionRegistry
{
    Task<Connection?> GetByIdAsync(Guid id);
    Task<Connection?> GetDefaultAsync();
    Task<IReadOnlyList<Connection>> ListAsync();
    Task<Connection> CreateAsync(CreateConnectionRequest request);
    Task<Connection?> UpdateAsync(Guid id, UpdateConnectionRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> TestConnectionAsync(Guid id);
}

public class CreateConnectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Platform { get; set; } = "SqlServer";
    public string Host { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

public class UpdateConnectionRequest
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? ConnectionString { get; set; }
    public string? Description { get; set; }
    public bool? IsDefault { get; set; }
}
