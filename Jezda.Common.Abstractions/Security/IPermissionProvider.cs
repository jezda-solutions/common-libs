using System;
using System.Collections.Generic;
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
    Task<List<string>> GetPermissionsAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get permission mask for user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <returns></returns>
    Task<long?> GetPermissionMaskAsync(
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
    /// Set permission mask for user in organisation.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <param name="permissionMask"></param>
    /// <param name="expiration"></param>
    /// <returns></returns>
    Task SetPermissionMaskAsync(
        Guid userId,
        Guid organisationId,
        long permissionMask,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove permission mask for user.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="organisationId"></param>
    /// <returns></returns>
    Task RemovePermissionMaskAsync(
        Guid userId,
        Guid organisationId,
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

