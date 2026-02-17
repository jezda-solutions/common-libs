using System;

namespace Jezda.Common.Domain.Enums;

/// <summary>
/// Defines specific roles of business organizations in retail/supply chain ecosystem.
/// Uses flags to allow organizations to have multiple simultaneous roles.
/// Examples: Metro (Wholesaler), NELT (Distributor), Bambi (Manufacturer)
///
/// For supply chain side (Supply/Buy), use <see cref="OrganisationSide"/> instead.
///
/// Historical value mapping (migration reference):
///   Old → New: Manufacturer(1→1), Distributor(2→2), Wholesaler(4→4),
///              LogisticsProvider(128→8), SoftwareProvider(1024→16)
///   Removed:   Retailer(8), OnlineRetailer(16), Supplier(256), Merchant(512),
///              Marketplace(32), Broker(64), DataProvider(2048), MarketingAgency(4096)
/// </summary>
[Flags]
public enum OrganisationType : int
{
    /// <summary>
    /// Organization type not defined or not applicable.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Produces finished goods from raw materials or components.
    /// Examples: Imlek, Bambi, Jaffa, Podravka, Atlantic Brands, Carnex
    /// </summary>
    Manufacturer = 1,

    /// <summary>
    /// Distributes products from manufacturers to merchants without significant transformation.
    /// Examples: NELT (Coca-Cola distributor), Delta DMD, STRAUSS ADRIATIC, Orbico
    /// </summary>
    Distributor = 2,

    /// <summary>
    /// Sells products in bulk quantities primarily to other businesses.
    /// Examples: Metro Cash &amp; Carry, Ruman Veleprodaja
    /// </summary>
    Wholesaler = 4,

    /// <summary>
    /// Provides logistics, warehousing, and fulfillment services.
    /// Examples: Third-party logistics providers, fulfillment centers
    /// </summary>
    LogisticsProvider = 8,

    /// <summary>
    /// Provides software solutions for retail/supply chain ecosystem.
    /// Examples: POS/ERP systems, SaaS inventory management, e-commerce platforms.
    /// </summary>
    SoftwareProvider = 16,

    /// <summary>
    /// Combination of all organization types — used for filtering/querying purposes.
    /// </summary>
    All = Manufacturer | Distributor | Wholesaler | LogisticsProvider | SoftwareProvider
}
