using Jezda.Common.Integrations.Abstractions.Enums;

namespace Jezda.Common.Integrations.Abstractions.Models;

public sealed class ExternalProjectDto
{
    public required string Id { get; init; }

    public required string Name { get; init; }
    
    public string? Description { get; init; }
    
    public string? Url { get; init; }
    
    public required ExternalProvider Provider { get; init; }
}
