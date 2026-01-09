using System.CommandLine;
using System.CommandLine.Invocation;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Snapshot;

/// <summary>
/// Command to create a database snapshot
/// </summary>
public class SnapshotCreateCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SnapshotCreateCommand"/> class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public SnapshotCreateCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<SnapshotCreateCommand> logger,
        CliConfiguration configuration)
        : base("create", "Create a database snapshot", apiClient, jsonHandler, logger, configuration)
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

        var descriptionOption = new Option<string?>(
            aliases: new[] { "--description" },
            description: "Optional description for the snapshot");

        var dataOption = CreateDataOption();
        var fileOption = CreateFileOption();

        AddOption(databaseOption);
        AddOption(nameOption);
        AddOption(descriptionOption);
        AddOption(dataOption);
        AddOption(fileOption);

        this.SetHandler(async (InvocationContext context) =>
        {
            var database = context.ParseResult.GetValueForOption(databaseOption);
            var name = context.ParseResult.GetValueForOption(nameOption);
            var description = context.ParseResult.GetValueForOption(descriptionOption);
            var data = context.ParseResult.GetValueForOption(dataOption);
            var file = context.ParseResult.GetValueForOption(fileOption);

            context.ExitCode = await ExecuteAsync(database!, name!, description, data, file);
        });
    }

    /// <summary>
    /// Executes the create snapshot command
    /// </summary>
    /// <param name="database">Database name</param>
    /// <param name="name">Snapshot name</param>
    /// <param name="description">Optional description</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">Path to JSON file</param>
    /// <returns>Exit code</returns>
    private async Task<int> ExecuteAsync(
        string database,
        string name,
        string? description,
        string? data,
        string? file)
    {
        try
        {
            // Build request object from CLI params
            var requestFromParams = new
            {
                Database = database,
                SnapshotName = name,
                Description = description
            };

            // Get final request using input precedence (data, file, stdin, params)
            var request = await JsonHandler.GetInputAsync(data, file, requestFromParams, CancellationToken.None);

            // Make API call
            var response = await ExecuteApiCallAsync<object, object>("/api/snapshot", request);
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
