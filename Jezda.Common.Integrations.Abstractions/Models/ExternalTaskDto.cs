using Jezda.Common.Integrations.Abstractions.Enums;

namespace Jezda.Common.Integrations.Abstractions.Models;

public sealed class ExternalTaskDto
{
    public required string Id { get; init; }
    
    public required string Title { get; init; }
    
    public string? Status { get; init; }
    
    public string? Url { get; init; }
    
    public required string ProjectId { get; init; }
    
    public required ExternalProvider Provider { get; init; }
}
