namespace cdc_cli.Services;

/// <summary>
/// Interface for HTTP client that communicates with the CDC API
/// </summary>
public interface ICdcApiClient
{
    /// <summary>
    /// Sends a POST request with JSON body and returns a typed response
    /// </summary>
    /// <typeparam name="TRequest">Type of the request object</typeparam>
    /// <typeparam name="TResponse">Type of the response object</typeparam>
    /// <param name="endpoint">API endpoint path (e.g., "api/cdc/start")</param>
    /// <param name="request">Request object to serialize as JSON</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized response object</returns>
    Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a GET request and returns a typed response
    /// </summary>
    /// <typeparam name="TResponse">Type of the response object</typeparam>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized response object</returns>
    Task<TResponse> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a DELETE request and returns a typed response
    /// </summary>
    /// <typeparam name="TResponse">Type of the response object</typeparam>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized response object</returns>
    Task<TResponse> DeleteAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken = default);
}
