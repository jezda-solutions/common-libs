using Jezda.Common.Integrations.Abstractions.Enums;

namespace Jezda.Common.Integrations.Abstractions.Models;

public sealed class ExternalProjectDto
{
    /// <summary>
    /// The provider's durable identifier: it survives a rename or a transfer, so this is the value
    /// to key stored rows on. GitHub repository id, Azure DevOps project GUID, Jira project id,
    /// ClickUp team id, Monday board id, Trello board id.
    /// </summary>
    /// <remarks>
    /// Before the durable id was introduced this held <see cref="Path"/> on GitHub, Azure DevOps and
    /// Jira, so a row stored from an older version holds a value that is no longer an <c>Id</c>.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// What the provider's API calls the project today: <c>owner/repo</c> on GitHub, the project
    /// name on Azure DevOps, the project key on Jira. Equal to <see cref="Id"/> where the provider
    /// has only one identifier (ClickUp, Monday, Trello).
    /// </summary>
    /// <remarks>
    /// It can change on the provider's side, so refresh it from <c>GetProjectsAsync</c> instead of
    /// treating it as a key.
    /// </remarks>
    public required string Path { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Url { get; init; }

    public required ExternalProvider Provider { get; init; }

    /// <summary>When the project was created. Null when the provider's list does not report it.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When the project last changed. Null when the provider's list does not report it.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Whether the project is closed for work. Null when the provider does not say.</summary>
    public bool? IsArchived { get; init; }

    /// <summary>Whether the project is hidden from the public. Null when the provider does not say.</summary>
    public bool? IsPrivate { get; init; }
}
