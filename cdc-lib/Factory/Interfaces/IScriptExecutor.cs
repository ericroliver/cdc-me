using System;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Executes a single hydration step (script) against a target database.
/// Implementations: SqlScriptExecutor (Phase 1), ProcessSpawnExecutor (future).
/// </summary>
public interface IScriptExecutor
{
    Task<ScriptResult> ExecuteAsync(
        Script script,
        System.Collections.Generic.IReadOnlyDictionary<string, object?> parameters,
        string connectionString);
}
