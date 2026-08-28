using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Softbase.Cdc.Factory.Interfaces;
using Softbase.Cdc.Factory.Models;

namespace Softbase.Cdc.Factory.Executors;

/// <summary>
/// SQL script executor: takes a script, substitutes ${ParamName} tokens
/// with values from the parameter bag, and executes via <see cref="IDatabaseProvider"/>.
/// </summary>
public class SqlScriptExecutor : IScriptExecutor
{
    private readonly IDatabaseProvider _databaseProvider;
    private readonly ILogger<SqlScriptExecutor> _logger;

    public SqlScriptExecutor(IDatabaseProvider databaseProvider, ILogger<SqlScriptExecutor> logger)
    {
        _databaseProvider = databaseProvider ?? throw new ArgumentNullException(nameof(databaseProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ScriptResult> ExecuteAsync(
        Script script,
        IReadOnlyDictionary<string, object?> parameters,
        string connectionString)
    {
        if (script is null)
            throw new ArgumentNullException(nameof(script));
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string is required", nameof(connectionString));

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get the SQL content — from inline Content or from FilePath
            string sql;
            if (!string.IsNullOrWhiteSpace(script.Content))
            {
                sql = script.Content;
            }
            else if (!string.IsNullOrWhiteSpace(script.FilePath))
            {
                if (!File.Exists(script.FilePath))
                    return ScriptResult.Fail(script.Name, $"Script file not found: {script.FilePath}", stopwatch.Elapsed);

                sql = await File.ReadAllTextAsync(script.FilePath);
            }
            else
            {
                return ScriptResult.Fail(script.Name, "Script has no content or file path", stopwatch.Elapsed);
            }

            // Substitute ${ParamName} tokens with parameter values
            sql = SubstituteParameters(sql, parameters);

            // Execute via the database provider
            var result = await _databaseProvider.ExecuteSqlAsync(connectionString, sql);

            stopwatch.Stop();

            if (!result.Success)
            {
                _logger.LogError("Script '{Name}' failed: {Error}", script.Name, result.ErrorMessage);
                return ScriptResult.Fail(script.Name, result.ErrorMessage ?? "Unknown error", stopwatch.Elapsed);
            }

            _logger.LogInformation(
                "Script '{Name}' executed successfully ({Rows} rows, {ElapsedMs}ms)",
                script.Name, result.RowsAffected, stopwatch.Elapsed.TotalMilliseconds);

            return ScriptResult.Ok(script.Name, result.RowsAffected, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Exception executing script '{Name}'", script.Name);
            return ScriptResult.Fail(script.Name, ex.Message, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Substitutes ${ParamName} tokens in SQL with values from the parameter bag.
    /// Exposed internally for testing.
    /// </summary>
    internal static string SubstituteParameters(string sql, IReadOnlyDictionary<string, object?> parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return sql;

        var result = sql;
        foreach (var (key, value) in parameters)
        {
            var token = $"${{{key}}}";
            var replacement = value?.ToString() ?? "";
            result = result.Replace(token, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
