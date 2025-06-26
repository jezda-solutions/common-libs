using System;
using System.Collections.Generic;

namespace Jezda.Common.Abstractions.Identity;

public interface IUserContext
{
    /// <summary>
    /// Id korisnikove organizacije
    /// </summary>
    Guid OrganisationId { get; }

    /// <summary>
    /// Korisnicki ID
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// Lista korisnikovih rola
    /// </summary>
    IEnumerable<string> UserRoles { get; }

    // TODO: we should change the types as some user claims may not be Guid?
    /// <summary>
    /// Dictionary na koji mapiramo sve relevantne claimove korisnika
    /// u ovom slucaju su to claimovi vezani samo za HRMS
    /// </summary>
    Dictionary<string, IEnumerable<Guid?>> UserClaims { get; }

    /// <summary>
    /// Ceo key value pair predstavlja claim.
    /// String je type i u ovom slucaju je to HRMS.Claims.User
    /// Lista guidova predstavlja sve organizacije koje su vezane za ovaj claim, te kasnije na osnovu
    /// toga izvlacimo trenutnu organizaciju
    /// </summary>
    KeyValuePair<string, IEnumerable<Guid?>> CurrentOrganisation { get; }
}