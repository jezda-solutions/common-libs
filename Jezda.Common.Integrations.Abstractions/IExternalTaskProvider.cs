using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;

namespace Jezda.Common.Integrations.Abstractions;

public interface IExternalTaskProvider
{
    ExternalProvider Provider { get; }

    Task<bool> ValidateConnectionAsync(
        string accessToken,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(
        string accessToken,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(
        string accessToken,
        string projectId,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);
}
