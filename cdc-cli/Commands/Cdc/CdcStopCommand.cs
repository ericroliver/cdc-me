using System.CommandLine;
using cdc_cli.Configuration;
using cdc_cli.Services;
using CdcModels;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Cdc;

/// <summary>
/// Command to stop CDC operations and capture data
/// </summary>
public class CdcStopCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the CdcStopCommand class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public CdcStopCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<CdcStopCommand> logger,
        CliConfiguration configuration)
        : base("stop", "Stop CDC operations and capture data", apiClient, jsonHandler, logger, configuration)
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
        var captureOption = new Option<string?>(
            aliases: new[] { "--capture", "-c" },
            description: "Name for this capture");

        var typeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            description: "Capture type (Baseline, Replay, Optimized, etc.)",
            getDefaultValue: () => "Baseline");

        var dataOption = CreateDataOption();
        var fileOption = CreateFileOption();

        // Add options to command
        AddOption(sessionOption);
        AddOption(captureOption);
        AddOption(typeOption);
        AddOption(dataOption);
        AddOption(fileOption);

        // Set handler
        this.SetHandler(async (session, capture, type, data, file) =>
        {
            await ExecuteAsync(session, capture, type, data, file);
        }, sessionOption, captureOption, typeOption, dataOption, fileOption);
    }

    /// <summary>
    /// Executes the stop CDC command
    /// </summary>
    /// <param name="session">Session name from CLI</param>
    /// <param name="capture">Capture name from CLI</param>
    /// <param name="type">Capture type from CLI</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">JSON file path</param>
    private async Task ExecuteAsync(
        string? session,
        string? capture,
        string type,
        string? data,
        string? file)
    {
        try
        {
            // Build request from input sources
            var request = await BuildRequestAsync(session, capture, type, data, file);
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

            if (string.IsNullOrWhiteSpace(request.CaptureName))
            {
                await HandleErrorAsync(
                    new InvalidOperationException("Capture name is required. Use --capture or provide JSON input with captureName."),
                    ExitCodeValidationError);
                return;
            }

            // Execute API call
            var response = await ExecuteApiCallAsync<StopCdcRequest, StopCdcResponse>("/api/cdc/stop", request);
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
    /// <param name="capture">Capture name from CLI</param>
    /// <param name="type">Capture type from CLI</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">JSON file path</param>
    /// <returns>Built request object or null if error occurred</returns>
    private async Task<StopCdcRequest?> BuildRequestAsync(
        string? session,
        string? capture,
        string type,
        string? data,
        string? file)
    {
        // Priority 1: --data (inline JSON)
        if (!string.IsNullOrWhiteSpace(data))
        {
            return await GetRequestAsync<StopCdcRequest>(data, null, false);
        }

        // Priority 2: --file (JSON from file)
        if (!string.IsNullOrWhiteSpace(file))
        {
            return await GetRequestAsync<StopCdcRequest>(null, file, false);
        }

        // Priority 3: stdin (if no CLI parameters provided and stdin is available)
        if (string.IsNullOrWhiteSpace(session) && string.IsNullOrWhiteSpace(capture))
        {
            if (!Console.IsInputRedirected)
            {
                await HandleErrorAsync(
                    new InvalidOperationException("No input provided. Use --session and --capture, or provide JSON via --data, --file, or stdin."),
                    ExitCodeValidationError);
                return null;
            }

            return await GetRequestAsync<StopCdcRequest>(null, null, true);
        }

        // Priority 4: CLI parameters
        return new StopCdcRequest
        {
            SessionName = session ?? string.Empty,
            CaptureName = capture ?? string.Empty,
            CaptureType = type
        };
    }
}
