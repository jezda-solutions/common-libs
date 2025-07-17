using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Abstractions.Security;

/// <summary>
/// Permission provider
/// </summary>
public interface IPermissionProvider
{
    /// <summary>
    /// Get permissions for user in organisation.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ImmutableHashSet<string>> GetPermissionsAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Has permission for user in organisation.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    Task<bool> HasPermissionAsync(
        Guid userId,
        Guid organisationId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add permission for user in organisation.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    Task AddPermissionAsync(
        Guid userId,
        Guid organisationId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove permission for user in organisation.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <param name="permission"></param>
    /// <returns></returns>
    Task RemovePermissionAsync(
        Guid userId,
        Guid organisationId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove all permissions for user.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task RemoveAllUserPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

