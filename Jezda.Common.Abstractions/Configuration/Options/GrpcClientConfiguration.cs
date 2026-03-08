namespace Jezda.Common.Abstractions.Configuration.Options;

/// <summary>
/// Configuration for a gRPC client connection.
/// Used by microservices to connect to gRPC services (e.g., Nexus).
/// </summary>
public class GrpcClientConfiguration
{
    /// <summary>
    /// The gRPC server URL (e.g., "https://nexus:5001").
    /// </summary>
    public required string Url { get; set; }

    /// <summary>
    /// API key for service-to-service authentication.
    /// Sent as metadata header on every gRPC call.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Metadata header name for the API key.
    /// </summary>
    public string ApiKeyHeaderName { get; set; } = "x-api-key";

    /// <summary>
    /// Request deadline in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to use TLS for the connection.
    /// </summary>
    public bool UseTls { get; set; } = true;
}
