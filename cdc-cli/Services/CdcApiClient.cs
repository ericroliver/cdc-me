using System.Net.Http.Json;
using System.Text.Json;
using cdc_cli.Configuration;
using Microsoft.Extensions.Logging;

namespace cdc_cli.Services;

/// <summary>
/// HTTP client implementation for communicating with the CDC API
/// </summary>
public class CdcApiClient : ICdcApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CdcApiClient> _logger;
    private readonly CliConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the CdcApiClient
    /// </summary>
    /// <param name="httpClient">HTTP client instance from factory</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">CLI configuration</param>
    public CdcApiClient(
        HttpClient httpClient,
        ILogger<CdcApiClient> logger,
        CliConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Configure HTTP client
        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        // Configure JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }

    /// <inheritdoc />
    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));
        }

        try
        {
            if (_configuration.Verbose)
            {
                _logger.LogDebug("POST {Endpoint}", endpoint);
                var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
                _logger.LogDebug("Request: {Request}", requestJson);
            }

            var response = await _httpClient.PostAsJsonAsync(
                endpoint.TrimStart('/'),
                request,
                _jsonOptions,
                cancellationToken);

            await EnsureSuccessStatusCodeWithDetails(response);

            var result = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Response deserialization returned null");
            }

            if (_configuration.Verbose)
            {
                var responseJson = JsonSerializer.Serialize(result, _jsonOptions);
                _logger.LogDebug("Response: {Response}", responseJson);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for endpoint: {Endpoint}", endpoint);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for endpoint: {Endpoint}", endpoint);
            throw new HttpRequestException($"Request to {endpoint} timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response from endpoint: {Endpoint}", endpoint);
            throw new InvalidOperationException($"Failed to parse JSON response from {endpoint}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TResponse> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));
        }

        try
        {
            if (_configuration.Verbose)
            {
                _logger.LogDebug("GET {Endpoint}", endpoint);
            }

            var response = await _httpClient.GetAsync(endpoint.TrimStart('/'), cancellationToken);

            await EnsureSuccessStatusCodeWithDetails(response);

            var result = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Response deserialization returned null");
            }

            if (_configuration.Verbose)
            {
                var responseJson = JsonSerializer.Serialize(result, _jsonOptions);
                _logger.LogDebug("Response: {Response}", responseJson);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for endpoint: {Endpoint}", endpoint);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for endpoint: {Endpoint}", endpoint);
            throw new HttpRequestException($"Request to {endpoint} timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response from endpoint: {Endpoint}", endpoint);
            throw new InvalidOperationException($"Failed to parse JSON response from {endpoint}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<TResponse> DeleteAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));
        }

        try
        {
            if (_configuration.Verbose)
            {
                _logger.LogDebug("DELETE {Endpoint}", endpoint);
            }

            var response = await _httpClient.DeleteAsync(endpoint.TrimStart('/'), cancellationToken);

            await EnsureSuccessStatusCodeWithDetails(response);

            var result = await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, cancellationToken);

            if (result == null)
            {
                throw new InvalidOperationException("Response deserialization returned null");
            }

            if (_configuration.Verbose)
            {
                var responseJson = JsonSerializer.Serialize(result, _jsonOptions);
                _logger.LogDebug("Response: {Response}", responseJson);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for endpoint: {Endpoint}", endpoint);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout for endpoint: {Endpoint}", endpoint);
            throw new HttpRequestException($"Request to {endpoint} timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize response from endpoint: {Endpoint}", endpoint);
            throw new InvalidOperationException($"Failed to parse JSON response from {endpoint}", ex);
        }
    }

    /// <summary>
    /// Ensures HTTP response indicates success, throwing exception with details if not
    /// </summary>
    /// <param name="response">HTTP response message</param>
    private async Task EnsureSuccessStatusCodeWithDetails(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;

        if (statusCode >= 400 && statusCode < 500)
        {
            // Client errors (4xx)
            throw new HttpRequestException(
                $"API request failed with status {statusCode}: {response.ReasonPhrase}. Details: {content}");
        }
        else if (statusCode >= 500)
        {
            // Server errors (5xx)
            throw new HttpRequestException(
                $"API server error with status {statusCode}: {response.ReasonPhrase}. Details: {content}");
        }
        else
        {
            // Other errors
            response.EnsureSuccessStatusCode();
        }
    }
}
