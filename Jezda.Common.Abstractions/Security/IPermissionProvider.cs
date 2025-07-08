using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Abstractions.Security;

public interface IPermissionProvider
{
    Task<List<string>> GetPermissionsAsync(Guid userId, Guid organisationId, CancellationToken cancellationToken = default);
}
