using System;
using System.Collections.Generic;

namespace Jezda.Common.Abstractions.Identity;

public interface IUserContext
{
    /// <summary>
    /// Current user id.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Current user email.
    /// </summary>
    string Email { get; }

    /// <summary>
    /// Current user name.
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// Id korisnikove organizacije
    /// </summary>
    Guid? CurrentOrganisationId { get; }

    /// <summary>
    /// Lista korisnikovih rola
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    // Fast checks
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    bool IsSupport { get; }

    // Claim access (optional)
    string? GetClaim(string type);

    IEnumerable<string> GetClaims(string type);

    string? BearerToken { get; }
}