using Jezda.Common.Integrations.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jezda.Common.Integrations.Clients;

/// <summary>
/// Base client for all external integrations.
/// Handles common HTTP operations, logging, and serialization.
/// </summary>
public abstract class BaseIntegrationClient
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly JsonSerializerOptions JsonSerializerOptions;

    protected BaseIntegrationClient(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Sends a GET request and returns the deserialized response.
    /// </summary>
    protected async Task<TResponse?> GetAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Sending GET request to {RequestUri}", requestUri);
            var response = await HttpClient.GetAsync(requestUri, cancellationToken);
            return await HandleResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while sending GET request to {RequestUri}", requestUri);
            throw;
        }
    }

    /// <summary>
    /// Sends a POST request with JSON payload and returns the deserialized response.
    /// </summary>
    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Sending POST request to {RequestUri}", requestUri);
            var response = await HttpClient.PostAsJsonAsync(requestUri, request, JsonSerializerOptions, cancellationToken);
            return await HandleResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while sending POST request to {RequestUri}", requestUri);
            throw;
        }
    }

    /// <summary>
    /// Sends a PUT request with JSON payload and returns the deserialized response.
    /// </summary>
    protected async Task<TResponse?> PutAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Sending PUT request to {RequestUri}", requestUri);
            var response = await HttpClient.PutAsJsonAsync(requestUri, request, JsonSerializerOptions, cancellationToken);
            return await HandleResponseAsync<TResponse>(response, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while sending PUT request to {RequestUri}", requestUri);
            throw;
        }
    }

    /// <summary>
    /// Sends a DELETE request.
    /// </summary>
    protected async Task DeleteAsync(string requestUri, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Sending DELETE request to {RequestUri}", requestUri);
            var response = await HttpClient.DeleteAsync(requestUri, cancellationToken);
            EnsureApiResponse(response);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsUtf8StringAsync(cancellationToken);
                Logger.LogError("Integration request failed. StatusCode: {StatusCode}, Content: {Content}", response.StatusCode, content);
                response.EnsureSuccessStatusCode();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error occurred while sending DELETE request to {RequestUri}", requestUri);
            throw;
        }
    }

    /// <summary>
    /// Rejects a response that its status alone would misreport, before anything reads it. Called for
    /// every response the helpers on this class receive, failed or not; the default accepts all.
    /// </summary>
    /// <remarks>
    /// For an API that can answer a rejected token with a success status, such as a sign-in page
    /// served as 203. Throw an <see cref="HttpRequestException"/> carrying the status callers should
    /// branch on. A client that sends requests of its own, without these helpers, calls it itself.
    /// </remarks>
    /// <param name="response">The response just received.</param>
    protected virtual void EnsureApiResponse(HttpResponseMessage response)
    {
    }

    private async Task<TResponse?> HandleResponseAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        EnsureApiResponse(response);

        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return default;
            }

            return await response.Content.ReadFromUtf8JsonAsync<TResponse>(JsonSerializerOptions, cancellationToken);
        }

        var content = await response.Content.ReadAsUtf8StringAsync(cancellationToken);
        Logger.LogError("Integration request failed. StatusCode: {StatusCode}, Content: {Content}", response.StatusCode, content);
        
        // Throw exception to be handled by caller or global handler
        // Or implement specific error handling strategy here
        response.EnsureSuccessStatusCode();
        
        return default; // Should not be reached
    }
}
