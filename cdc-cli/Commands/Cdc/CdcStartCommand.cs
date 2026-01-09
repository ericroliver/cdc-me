using System.CommandLine;
using cdc_cli.Configuration;
using cdc_cli.Services;
using CdcModels;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Cdc;

/// <summary>
/// Command to start CDC operations on database tables
/// </summary>
public class CdcStartCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the CdcStartCommand class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public CdcStartCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<CdcStartCommand> logger,
        CliConfiguration configuration)
        : base("start", "Start CDC operations on database tables", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Create options
        var sessionOption = CreateSessionOption(required: false);
        var includeOption = new Option<string[]?>(
            aliases: new[] { "--include", "-i" },
            description: "Tables to include in CDC (can be specified multiple times, e.g., dbo.Orders)")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore
        };

        var excludeOption = new Option<string[]?>(
            aliases: new[] { "--exclude", "-e" },
            description: "Tables to exclude from CDC (can be specified multiple times)")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore
        };

        var dataOption = CreateDataOption();
        var fileOption = CreateFileOption();

        // Add options to command
        AddOption(sessionOption);
        AddOption(includeOption);
        AddOption(excludeOption);
        AddOption(dataOption);
        AddOption(fileOption);

        // Set handler
        this.SetHandler(async (session, include, exclude, data, file) =>
        {
            await ExecuteAsync(session, include, exclude, data, file);
        }, sessionOption, includeOption, excludeOption, dataOption, fileOption);
    }

    /// <summary>
    /// Executes the start CDC command
    /// </summary>
    /// <param name="session">Session name from CLI</param>
    /// <param name="include">Tables to include</param>
    /// <param name="exclude">Tables to exclude</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">JSON file path</param>
    private async Task ExecuteAsync(
        string? session,
        string[]? include,
        string[]? exclude,
        string? data,
        string? file)
    {
        try
        {
            // Build request from input sources
            var request = await BuildRequestAsync(session, include, exclude, data, file);
            if (request == null)
            {
                return; // Error already handled
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.SessionName))
            {
                await HandleErrorAsync(
                    new InvalidOperationException("Session name is required. Use --session or provide JSON input with sessionName."),
                    ExitCodeValidationError);
                return;
            }

            // Execute API call
            var response = await ExecuteApiCallAsync<StartCdcRequest, StartCdcResponse>("/api/cdc/start", request);
            if (response == null)
            {
                return; // Error already handled
            }

            // Write response
            await WriteResponseAsync(response);
            
            // Set exit code based on success
            Environment.ExitCode = response.Success ? ExitCodeSuccess : ExitCodeApiError;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    /// <summary>
    /// Builds the request from various input sources
    /// </summary>
    /// <param name="session">Session name from CLI</param>
    /// <param name="include">Tables to include</param>
    /// <param name="exclude">Tables to exclude</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">JSON file path</param>
    /// <returns>Built request object or null if error occurred</returns>
    private async Task<StartCdcRequest?> BuildRequestAsync(
        string? session,
        string[]? include,
        string[]? exclude,
        string? data,
        string? file)
    {
        // Priority 1: --data (inline JSON)
        if (!string.IsNullOrWhiteSpace(data))
        {
            return await GetRequestAsync<StartCdcRequest>(data, null, false);
        }

        // Priority 2: --file (JSON from file)
        if (!string.IsNullOrWhiteSpace(file))
        {
            return await GetRequestAsync<StartCdcRequest>(null, file, false);
        }

        // Priority 3: stdin (if no CLI parameters provided and stdin is available)
        if (string.IsNullOrWhiteSpace(session) && (include == null || include.Length == 0) && (exclude == null || exclude.Length == 0))
        {
            if (!Console.IsInputRedirected)
            {
                await HandleErrorAsync(
                    new InvalidOperationException("No input provided. Use --session with --include/--exclude, or provide JSON via --data, --file, or stdin."),
                    ExitCodeValidationError);
                return null;
            }

            return await GetRequestAsync<StartCdcRequest>(null, null, true);
        }

        // Priority 4: CLI parameters
        return new StartCdcRequest
        {
            SessionName = session ?? string.Empty,
            TablesToInclude = include?.ToList(),
            TablesToExclude = exclude?.ToList()
        };
    }
}
