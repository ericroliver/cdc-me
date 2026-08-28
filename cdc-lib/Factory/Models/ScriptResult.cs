using System;
using System.Threading.Tasks;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// Result of executing a single script.
/// </summary>
public class ScriptResult
{
    public bool Success { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public int RowsAffected { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ExecutionTime { get; set; }

    public static ScriptResult Ok(string name, int rowsAffected, TimeSpan elapsed) => new()
    {
        Success = true,
        ScriptName = name,
        RowsAffected = rowsAffected,
        ExecutionTime = elapsed
    };

    public static ScriptResult Fail(string name, string error, TimeSpan elapsed) => new()
    {
        Success = false,
        ScriptName = name,
        ErrorMessage = error,
        ExecutionTime = elapsed
    };
}
