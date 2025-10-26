using System;

namespace Jezda.Common.Domain.Enums;

/// <summary>
/// Defines types of business organizations in retail/supply chain ecosystem.
/// Uses flags to allow organizations to have multiple simultaneous roles.
/// Examples: Tesla (Manufacturer | Retailer), Amazon (OnlineRetailer | Marketplace | Distributor)
/// </summary>
[Flags]
public enum OrganisationType : int
{
    /// <summary>
    /// Organization type not defined or not applicable
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Produces finished goods from raw materials or components.
    /// Examples: Imlek, Bambi, Jaffa, Podravka, Atlantic Brands, Carnex
    /// </summary>
    Manufacturer = 1,

    /// <summary>
    /// Distributes products from manufacturers to retailers without significant transformation.
    /// Examples: NELT (Coca-Cola distributor), Delta DMD, STRAUSS ADRIATIC, Orbico
    /// </summary>
    Distributor = 2,

    /// <summary>
    /// Sells products in bulk quantities primarily to other businesses.
    /// Examples: Metro Cash & Carry, Ruman Veleprodaja
    /// </summary>
    Wholesaler = 4,

    /// <summary>
    /// Sells products directly to end consumers through physical stores.
    /// Examples: IDEA, LIDL, MAXI, Bisa Market, Domino Market
    /// </summary>
    Retailer = 8,

    /// <summary>
    /// Sells products directly to consumers through online channels.
    /// Examples: Online stores, e-commerce platforms
    /// </summary>
    OnlineRetailer = 16,

    /// <summary>
    /// Platform that enables multiple sellers to reach customers.
    /// Examples: Amazon Marketplace, eBay, local marketplace platforms
    /// </summary>
    Marketplace = 32,

    /// <summary>
    /// Facilitates transactions between parties without holding inventory.
    /// Examples: Trading companies, business brokers
    /// </summary>
    Broker = 64,

    /// <summary>
    /// Provides logistics, warehousing, and fulfillment services.
    /// Examples: Third-party logistics providers, fulfillment centers
    /// </summary>
    LogisticsProvider = 128,

    /// <summary>
    /// Legacy term - Generic supplier providing goods or services to other businesses.
    /// Maintained for backward compatibility. Consider using more specific types.
    /// </summary>
    Supplier = 256,

    /// <summary>
    /// Legacy term - Generic vendor selling products or services.
    /// Maintained for backward compatibility. Consider using more specific types.
    /// </summary>
    Vendor = 512,

    /// <summary>
    /// Provides software solutions for retail/supply chain ecosystem.
    /// Examples: POS/ERP vendors, SaaS inventory management, e-commerce platforms.
    /// </summary>
    SoftwareProvider = 1024,

    /// <summary>
    /// Provides product data feeds, catalogs, and master data services (e.g., GS1).
    /// </summary>
    DataProvider = 2048,

    /// <summary>
    /// Provides marketing and advertising services.
    /// </summary>
    MarketingAgency = 4096,

    /// <summary>
    /// Combination of all organization types - used for filtering/querying purposes
    /// </summary>
    All = Manufacturer | Distributor | Wholesaler | Retailer | OnlineRetailer |
          Marketplace | Broker | LogisticsProvider | Supplier | Vendor | SoftwareProvider |
          DataProvider | MarketingAgency
}