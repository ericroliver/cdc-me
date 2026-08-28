using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Audit registry of every database DTAI has provisioned.
/// Read-only in Phase 1; the factory engine creates entries.
/// </summary>
public interface IDatabaseRegistry
{
    Task<IReadOnlyList<ProvisionedDatabase>> ListAsync();
    Task<ProvisionedDatabase?> GetByIdAsync(Guid id);
}
