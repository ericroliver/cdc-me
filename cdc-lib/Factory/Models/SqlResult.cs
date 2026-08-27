using System;
using System.Threading.Tasks;

namespace Softbase.Cdc.Factory.Models;

/// <summary>
/// Result of a database operation (restore, create, drop, execute SQL).
/// </summary>
public class SqlResult
{
    public bool Success { get; set; }
    public int RowsAffected { get; set; }
    public string? ErrorMessage { get; set; }

    public static SqlResult Ok(int rowsAffected = 0) => new()
    {
        Success = true,
        RowsAffected = rowsAffected
    };

    public static SqlResult Fail(string error) => new()
    {
        Success = false,
        ErrorMessage = error
    };
}
