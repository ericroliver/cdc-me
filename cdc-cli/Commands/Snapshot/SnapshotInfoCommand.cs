using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Snapshot;

/// <summary>
/// Command to get detailed information about a snapshot
/// </summary>
public class SnapshotInfoCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotInfoCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public SnapshotInfoCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<SnapshotInfoCommand> logger,
        CliConfiguration configuration)
        : base("info", "Get detailed information about a snapshot", apiClient, jsonHandler, logger, configuration)
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

        var nameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Snapshot name (required)")
        {
            IsRequired = true
        };

        AddOption(databaseOption);
        AddOption(nameOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var database = context.ParseResult.GetValueForOption(databaseOption);
            var name = context.ParseResult.GetValueForOption(nameOption);
            context.ExitCode = await ExecuteAsync(database!, name!);
        });
    }

    /// <summary>
    /// Executes the snapshot info command
    /// </summary>
    /// <param name="database">Database name</param>
    /// <param name="name">Snapshot name</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string database, string name)
    {
        try
        {
            // Make API call
            var response = await ExecuteGetAsync<object>($"/api/snapshot/{database}/snapshots/{name}");
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
