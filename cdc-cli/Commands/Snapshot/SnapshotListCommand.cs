using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Snapshot;

/// <summary>
/// Command to list database snapshots
/// </summary>
public class SnapshotListCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotListCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public SnapshotListCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<SnapshotListCommand> logger,
        CliConfiguration configuration)
        : base("list", "List all snapshots for a database", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add options
        var databaseOption = new Option<string>(
            aliases: new[] { "--database", "-d" },
            description: "Database name (required)")
        {
            IsRequired = true
        };

        AddOption(databaseOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var database = context.ParseResult.GetValueForOption(databaseOption);
            context.ExitCode = await ExecuteAsync(database!);
        });
    }

    /// <summary>
    /// Executes the list snapshots command
    /// </summary>
    /// <param name="database">Database name</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string database)
    {
        try
        {
            // Make API call
            var response = await ExecuteGetAsync<object>($"/api/snapshot/{database}/snapshots");
            if (response == null)
            {
                return ExitCodeApiError; // Error already handled
            }

            // Output response
            await WriteResponseAsync(response);
            return ExitCodeSuccess;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            return ExitCodeApiError;
        }
    }
}
