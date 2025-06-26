namespace Jezda.Common.Abstractions.Configuration.Options;

public class NexusOptions
{
    public required string BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string? ApiVersion { get; set; } = "v1";

    public string? ApiKeyHeaderName { get; set; } = "X-API-Key";
}
