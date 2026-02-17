using System;

namespace Jezda.Common.Domain.Enums;

/// <summary>
/// Defines which side of the supply chain an organization operates on.
/// Uses flags to allow organizations to operate on both sides simultaneously.
/// Examples: Metro (Supply | Buy), NELT (Supply), IDEA (Buy)
/// </summary>
[Flags]
public enum OrganisationSide
{
    /// <summary>
    /// Side not defined or not applicable.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Supply-side: organization provides goods or services to other businesses.
    /// Examples: manufacturers, distributors, wholesalers, logistics providers.
    /// </summary>
    Supply = 1,

    /// <summary>
    /// Buy-side: organization purchases goods for resale to end consumers.
    /// Examples: retail chains, online stores, merchants.
    /// </summary>
    Buy = 2
}
