using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Snapshot;

/// <summary>
/// Command to delete a database snapshot
/// </summary>
public class SnapshotDeleteCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotDeleteCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public SnapshotDeleteCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<SnapshotDeleteCommand> logger,
        CliConfiguration configuration)
        : base("delete", "Delete a database snapshot", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Add options
        var nameOption = new Option<string>(
            aliases: new[] { "--name", "-n" },
            description: "Snapshot name (required)")
        {
            IsRequired = true
        };

        var forceOption = new Option<bool>(
            aliases: new[] { "--force" },
            description: "Skip confirmation prompt",
            getDefaultValue: () => false);

        AddOption(nameOption);
        AddOption(forceOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var name = context.ParseResult.GetValueForOption(nameOption);
            var force = context.ParseResult.GetValueForOption(forceOption);
            context.ExitCode = await ExecuteAsync(name!, force);
        });
    }

    /// <summary>
    /// Executes the delete snapshot command
    /// </summary>
    /// <param name="name">Snapshot name</param>
    /// <param name="force">Whether to skip confirmation</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(string name, bool force)
    {
        try
        {
            // Interactive confirmation if force flag not provided
            if (!force && !Configuration.Quiet)
            {
                Console.WriteLine($"WARNING: You are about to permanently delete snapshot '{name}'.");
                Console.Write("Are you sure you want to continue? (y/N): ");
                var userResponse = Console.ReadLine()?.Trim().ToLowerInvariant();
                
                if (userResponse != "y" && userResponse != "yes")
                {
                    Console.WriteLine("Delete cancelled.");
                    return ExitCodeSuccess; // User cancelled, not an error
                }
            }

            // Make API call
            var response = await ExecuteDeleteAsync<object>($"/api/snapshot/{name}");
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
