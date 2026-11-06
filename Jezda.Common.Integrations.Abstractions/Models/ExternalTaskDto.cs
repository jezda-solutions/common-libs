using Jezda.Common.Integrations.Abstractions.Enums;

namespace Jezda.Common.Integrations.Abstractions.Models;

public sealed class ExternalTaskDto
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Status { get; init; }

    public string? Url { get; init; }

    /// <summary>
    /// The owning project's durable id, equal to <see cref="ExternalProjectDto.Id"/>. Bind the task
    /// to its project by this value.
    /// </summary>
    public required string ProjectId { get; init; }

    /// <summary>
    /// The owning project's current <see cref="ExternalProjectDto.Path"/>.
    /// </summary>
    public required string ProjectPath { get; init; }

    public required ExternalProvider Provider { get; init; }
}
