using System.CommandLine;
using cdc_cli.Configuration;
using cdc_cli.Services;
using CdcModels;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands.Cdc;

/// <summary>
/// Command to compare two CDC captures
/// </summary>
public class CdcCompareCommand : ApiCommandBase
{
    /// <summary>
    /// Initializes a new instance of the CdcCompareCommand class
    /// </summary>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public CdcCompareCommand(
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger<CdcCompareCommand> logger,
        CliConfiguration configuration)
        : base("compare", "Compare two CDC captures to validate identical data changes", apiClient, jsonHandler, logger, configuration)
    {
        ConfigureCommand();
    }

    /// <summary>
    /// Configures command options and handler
    /// </summary>
    private void ConfigureCommand()
    {
        // Create options
        var baselineOption = new Option<string?>(
            aliases: new[] { "--baseline", "-b" },
            description: "Name of the baseline/expected capture");

        var testOption = new Option<string?>(
            aliases: new[] { "--test", "-t" },
            description: "Name of the test capture to compare against baseline");

        var fieldsToIgnoreOption = new Option<string[]?>(
            aliases: new[] { "--fields-to-ignore", "-i" },
            description: "List of field names to ignore during comparison (e.g., timestamps)");

        var ignoreLsnOption = new Option<bool>(
            aliases: new[] { "--ignore-lsn", "-l" },
            description: "Ignore LSN (Log Sequence Number) differences",
            getDefaultValue: () => true);

        var dataOption = CreateDataOption();
        var fileOption = CreateFileOption();

        // Add options to command
        AddOption(baselineOption);
        AddOption(testOption);
        AddOption(fieldsToIgnoreOption);
        AddOption(ignoreLsnOption);
        AddOption(dataOption);
        AddOption(fileOption);

        // Set handler
        this.SetHandler(async (baseline, test, fieldsToIgnore, ignoreLsn, data, file) =>
        {
            await ExecuteAsync(baseline, test, fieldsToIgnore, ignoreLsn, data, file);
        }, baselineOption, testOption, fieldsToIgnoreOption, ignoreLsnOption, dataOption, fileOption);
    }

    /// <summary>
    /// Executes the compare CDC command
    /// </summary>
    /// <param name="baseline">Baseline capture name from CLI</param>
    /// <param name="test">Test capture name from CLI</param>
    /// <param name="fieldsToIgnore">Fields to ignore from CLI</param>
    /// <param name="ignoreLsn">Whether to ignore LSN differences</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">JSON file path</param>
    private async Task ExecuteAsync(
        string? baseline,
        string? test,
        string[]? fieldsToIgnore,
        bool ignoreLsn,
        string? data,
        string? file)
    {
        try
        {
            // Build request from input sources
            var request = await BuildRequestAsync(baseline, test, fieldsToIgnore, ignoreLsn, data, file);
            if (request == null)
            {
                return; // Error already handled
            }

            // Validate request
            if (string.IsNullOrWhiteSpace(request.BaselineCaptureName))
            {
                await HandleErrorAsync(
                    new InvalidOperationException("Baseline capture name is required. Use --baseline or provide JSON input with baselineCaptureName."),
                    ExitCodeValidationError);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.TestCaptureName))
            {
                await HandleErrorAsync(
                    new InvalidOperationException("Test capture name is required. Use --test or provide JSON input with testCaptureName."),
                    ExitCodeValidationError);
                return;
            }

            // Execute API call
            var response = await ExecuteApiCallAsync<CompareCapturesRequest, CompareCapturesResponse>("/api/cdc/compare", request);
            if (response == null)
            {
                return; // Error already handled
            }

            // Write response
            await WriteResponseAsync(response);

            // Set exit code based on match result
            // Exit 0 if captures match, exit 1 if they don't match (but no errors), exit 2 if errors occurred
            if (response.Errors.Any())
            {
                Environment.ExitCode = ExitCodeApiError;
            }
            else if (!response.IsMatch)
            {
                Environment.ExitCode = 1; // Captures don't match
            }
            else
            {
                Environment.ExitCode = ExitCodeSuccess;
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    /// <summary>
    /// Builds the request from various input sources
    /// </summary>
    /// <param name="baseline">Baseline capture name from CLI</param>
    /// <param name="test">Test capture name from CLI</param>
    /// <param name="fieldsToIgnore">Fields to ignore from CLI</param>
    /// <param name="ignoreLsn">Whether to ignore LSN differences</param>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">JSON file path</param>
    /// <returns>Built request object or null if error occurred</returns>
    private async Task<CompareCapturesRequest?> BuildRequestAsync(
        string? baseline,
        string? test,
        string[]? fieldsToIgnore,
        bool ignoreLsn,
        string? data,
        string? file)
    {
        // Priority 1: --data (inline JSON)
        if (!string.IsNullOrWhiteSpace(data))
        {
            return await GetRequestAsync<CompareCapturesRequest>(data, null, false);
        }

        // Priority 2: --file (JSON from file)
        if (!string.IsNullOrWhiteSpace(file))
        {
            return await GetRequestAsync<CompareCapturesRequest>(null, file, false);
        }

        // Priority 3: stdin (if no CLI parameters provided and stdin is available)
        if (string.IsNullOrWhiteSpace(baseline) && string.IsNullOrWhiteSpace(test))
        {
            if (!Console.IsInputRedirected)
            {
                await HandleErrorAsync(
                    new InvalidOperationException("No input provided. Use --baseline and --test, or provide JSON via --data, --file, or stdin."),
                    ExitCodeValidationError);
                return null;
            }

            return await GetRequestAsync<CompareCapturesRequest>(null, null, true);
        }

        // Priority 4: CLI parameters
        return new CompareCapturesRequest
        {
            BaselineCaptureName = baseline ?? string.Empty,
            TestCaptureName = test ?? string.Empty,
            FieldsToIgnore = fieldsToIgnore?.ToList(),
            IgnoreLsnDifferences = ignoreLsn
        };
    }
}
