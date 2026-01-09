using System.CommandLine;
using cdc_cli.Configuration;
using cdc_cli.Services;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Commands;

/// <summary>
/// Base class for all API commands providing common functionality
/// </summary>
public abstract class ApiCommandBase : Command
{
    /// <summary>
    /// HTTP API client for making requests
    /// </summary>
    protected readonly ICdcApiClient ApiClient;

    /// <summary>
    /// JSON handler for input/output operations
    /// </summary>
    protected readonly IJsonHandler JsonHandler;

    /// <summary>
    /// Logger instance
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// CLI configuration settings
    /// </summary>
    protected readonly CliConfiguration Configuration;

    /// <summary>
    /// Exit code for successful operations
    /// </summary>
    protected const int ExitCodeSuccess = 0;

    /// <summary>
    /// Exit code for API/request errors
    /// </summary>
    protected const int ExitCodeApiError = 1;

    /// <summary>
    /// Exit code for file I/O errors
    /// </summary>
    protected const int ExitCodeFileError = 2;

    /// <summary>
    /// Exit code for validation errors
    /// </summary>
    protected const int ExitCodeValidationError = 3;

    /// <summary>
    /// Initializes a new instance of the ApiCommandBase class
    /// </summary>
    /// <param name="name">Command name</param>
    /// <param name="description">Command description</param>
    /// <param name="apiClient">HTTP API client</param>
    /// <param name="jsonHandler">JSON handler</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    protected ApiCommandBase(
        string name,
        string description,
        ICdcApiClient apiClient,
        IJsonHandler jsonHandler,
        ILogger logger,
        CliConfiguration configuration)
        : base(name, description)
    {
        ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        JsonHandler = jsonHandler ?? throw new ArgumentNullException(nameof(jsonHandler));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Creates a standard --data option for inline JSON input
    /// </summary>
    /// <returns>Option for inline JSON data</returns>
    protected Option<string?> CreateDataOption()
    {
        return new Option<string?>(
            aliases: new[] { "--data", "-d" },
            description: "Inline JSON data for the request");
    }

    /// <summary>
    /// Creates a standard --file option for JSON file input
    /// </summary>
    /// <returns>Option for JSON file path</returns>
    protected Option<string?> CreateFileOption()
    {
        return new Option<string?>(
            aliases: new[] { "--file", "-f" },
            description: "Path to JSON file containing request data");
    }

    /// <summary>
    /// Creates a standard --session option for session name
    /// </summary>
    /// <returns>Option for session name</returns>
    protected Option<string> CreateSessionOption(bool required = true)
    {
        var option = new Option<string>(
            aliases: new[] { "--session", "-s" },
            description: "Name of the CDC session");
        option.IsRequired = required;
        return option;
    }

    /// <summary>
    /// Reads request data from various input sources
    /// </summary>
    /// <typeparam name="TRequest">Type of request object</typeparam>
    /// <param name="data">Inline JSON data</param>
    /// <param name="file">File path</param>
    /// <param name="allowStdin">Whether to allow reading from stdin</param>
    /// <returns>Deserialized request object or null if no input</returns>
    protected async Task<TRequest?> GetRequestAsync<TRequest>(
        string? data,
        string? file,
        bool allowStdin = true) where TRequest : class
    {
        try
        {
            return await JsonHandler.ReadInputAsync<TRequest>(data, file, allowStdin);
        }
        catch (FileNotFoundException ex)
        {
            await HandleErrorAsync(ex, ExitCodeFileError);
            return null;
        }
        catch (IOException ex)
        {
            await HandleErrorAsync(ex, ExitCodeFileError);
            return null;
        }
        catch (InvalidOperationException ex) when (ex.InnerException is System.Text.Json.JsonException)
        {
            await HandleErrorAsync(ex, ExitCodeValidationError);
            return null;
        }
    }

    /// <summary>
    /// Executes an API call with standard error handling
    /// </summary>
    /// <typeparam name="TRequest">Type of request object</typeparam>
    /// <typeparam name="TResponse">Type of response object</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <param name="request">Request object</param>
    /// <returns>Response object or default if error occurred</returns>
    protected async Task<TResponse?> ExecuteApiCallAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request)
    {
        try
        {
            return await ApiClient.PostAsync<TRequest, TResponse>(endpoint, request);
        }
        catch (HttpRequestException ex)
        {
            await HandleErrorAsync(ex, ExitCodeApiError);
            return default;
        }
        catch (InvalidOperationException ex)
        {
            await HandleErrorAsync(ex, ExitCodeApiError);
            return default;
        }
    }

    /// <summary>
    /// Executes a GET API call with standard error handling
    /// </summary>
    /// <typeparam name="TResponse">Type of response object</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <returns>Response object or default if error occurred</returns>
    protected async Task<TResponse?> ExecuteGetAsync<TResponse>(string endpoint)
    {
        try
        {
            return await ApiClient.GetAsync<TResponse>(endpoint);
        }
        catch (HttpRequestException ex)
        {
            await HandleErrorAsync(ex, ExitCodeApiError);
            return default;
        }
        catch (InvalidOperationException ex)
        {
            await HandleErrorAsync(ex, ExitCodeApiError);
            return default;
        }
    }

    /// <summary>
    /// Executes a DELETE API call with standard error handling
    /// </summary>
    /// <typeparam name="TResponse">Type of response object</typeparam>
    /// <param name="endpoint">API endpoint</param>
    /// <returns>Response object or default if error occurred</returns>
    protected async Task<TResponse?> ExecuteDeleteAsync<TResponse>(string endpoint)
    {
        try
        {
            return await ApiClient.DeleteAsync<TResponse>(endpoint);
        }
        catch (HttpRequestException ex)
        {
            await HandleErrorAsync(ex, ExitCodeApiError);
            return default;
        }
        catch (InvalidOperationException ex)
        {
            await HandleErrorAsync(ex, ExitCodeApiError);
            return default;
        }
    }

    /// <summary>
    /// Handles errors with appropriate logging and user feedback
    /// </summary>
    /// <param name="exception">Exception that occurred</param>
    /// <param name="exitCode">Exit code to use</param>
    protected async Task HandleErrorAsync(Exception exception, int exitCode = ExitCodeApiError)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        // Determine error type and message
        string errorMessage = exception switch
        {
            HttpRequestException => $"API request failed: {exception.Message}",
            FileNotFoundException => $"File not found: {exception.Message}",
            IOException => $"File I/O error: {exception.Message}",
            InvalidOperationException when exception.InnerException is System.Text.Json.JsonException =>
                $"Invalid JSON: {exception.Message}",
            _ => $"Unexpected error: {exception.Message}"
        };

        // Log the full exception in verbose mode
        if (Configuration.Verbose)
        {
            Logger.LogError(exception, "Command execution failed");
        }
        else
        {
            Logger.LogError(errorMessage);
        }

        // Write error to stderr
        await JsonHandler.WriteErrorAsync(errorMessage, exitCode);
    }

    /// <summary>
    /// Writes the response output in the configured format
    /// </summary>
    /// <typeparam name="TResponse">Type of response object</typeparam>
    /// <param name="response">Response object to write</param>
    protected async Task WriteResponseAsync<TResponse>(TResponse response)
    {
        if (response == null)
        {
            throw new ArgumentNullException(nameof(response));
        }

        await JsonHandler.WriteOutputAsync(response, Configuration.OutputFormat);
    }

    /// <summary>
    /// Sets the exit code to success
    /// </summary>
    protected void SetSuccessExitCode()
    {
        Environment.ExitCode = ExitCodeSuccess;
    }
}
