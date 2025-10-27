using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Jezda.Common.Domain.Constants;

public static class PermissionConstants
{
    public static string PermissionType => "permission";

    public static List<string> GetRegisteredPermissions()
    {
        var permissions = new List<string>();

        // Get all nested types (like Nexus, Retail, TMS, etc.)
        var nestedTypes = typeof(PermissionConstants).GetNestedTypes();

        foreach (var nestedType in nestedTypes)
        {
            // For each nested type, get all nested types within it (like Nexus.Users, Retail.Brands, etc.)
            var innerNestedTypes = nestedType.GetNestedTypes();

            foreach (var innerType in innerNestedTypes)
            {
                // Get all public static const string fields
                var fields = innerType.GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.IsLiteral && f.FieldType == typeof(string)); // IsLiteral means const

                foreach (var field in fields)
                {
                    var value = field.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(value) && !permissions.Contains(value))
                    {
                        permissions.Add(value);
                    }
                }
            }
        }

        return permissions;
    }

    public static class Retail
    {
        [DisplayName("Addresses")]
        public static class Addresses
        {
            public const string View = "retail:addresses:view";
            public const string Search = "retail:addresses:search";
            public const string Create = "retail:addresses:create";
            public const string Update = "retail:addresses:update";
            public const string Delete = "retail:addresses:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Addresses));
        }

        [DisplayName("AppLogs")]
        public static class AppLogs
        {
            public const string View = "retail:app-logs:view";
            public const string Search = "retail:app-logs:search";
            public const string Create = "retail:app-logs:create";
            public const string Update = "retail:app-logs:update";
            public const string Delete = "retail:app-logs:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(AppLogs));
        }

        [DisplayName("BaseProducts")]
        public static class BaseProducts
        {
            public const string View = "retail:base-products:view";
            public const string Search = "retail:base-products:search";
            public const string Create = "retail:base-products:create";
            public const string Update = "retail:base-products:update";
            public const string Delete = "retail:base-products:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProducts));
        }

        [DisplayName("BaseProductBarcodes")]
        public static class BaseProductBarcodes
        {
            public const string View = "retail:base-product-barcodes:view";
            public const string Search = "retail:base-product-barcodes:search";
            public const string Create = "retail:base-product-barcodes:create";
            public const string Update = "retail:base-product-barcodes:update";
            public const string Delete = "retail:base-product-barcodes:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProductBarcodes));
        }

        [DisplayName("BaseProductCategories")]
        public static class BaseProductCategories
        {
            public const string View = "retail:base-product-categories:view";
            public const string Search = "retail:base-product-categories:search";
            public const string Create = "retail:base-product-categories:create";
            public const string Update = "retail:base-product-categories:update";
            public const string Delete = "retail:base-product-categories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProductCategories));
        }

        [DisplayName("BaseProductCategoryRelations")]
        public static class BaseProductCategoryRelations
        {
            public const string View = "retail:base-product-category-relations:view";
            public const string Search = "retail:base-product-category-relations:search";
            public const string Create = "retail:base-product-category-relations:create";
            public const string Update = "retail:base-product-category-relations:update";
            public const string Delete = "retail:base-product-category-relations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProductCategoryRelations));
        }

        [DisplayName("BaseProductDimensions")]
        public static class BaseProductDimensions
        {
            public const string View = "retail:base-product-dimensions:view";
            public const string Search = "retail:base-product-dimensions:search";
            public const string Create = "retail:base-product-dimensions:create";
            public const string Update = "retail:base-product-dimensions:update";
            public const string Delete = "retail:base-product-dimensions:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProductDimensions));
        }

        [DisplayName("BaseProductImages")]
        public static class BaseProductImages
        {
            public const string View = "retail:base-product-images:view";
            public const string Search = "retail:base-product-images:search";
            public const string Create = "retail:base-product-images:create";
            public const string Update = "retail:base-product-images:update";
            public const string Delete = "retail:base-product-images:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProductImages));
        }

        [DisplayName("BillingInfos")]
        public static class BillingInfos
        {
            public const string View = "retail:billing-infos:view";
            public const string Search = "retail:billing-infos:search";
            public const string Create = "retail:billing-infos:create";
            public const string Update = "retail:billing-infos:update";
            public const string Delete = "retail:billing-infos:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BillingInfos));
        }

        [DisplayName("Brands")]
        public static class Brands
        {
            public const string View = "retail:brands:view";
            public const string Search = "retail:brands:search";
            public const string Create = "retail:brands:create";
            public const string Update = "retail:brands:update";
            public const string Delete = "retail:brands:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Brands));
        }

        [DisplayName("Categories")]
        public static class Categories
        {
            public const string View = "retail:categories:view";
            public const string Search = "retail:categories:search";
            public const string Create = "retail:categories:create";
            public const string Update = "retail:categories:update";
            public const string Delete = "retail:categories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Categories));
        }

        [DisplayName("CrawlArticles")]
        public static class CrawlArticles
        {
            public const string View = "retail:crawl-articles:view";
            public const string Search = "retail:crawl-articles:search";
            public const string Create = "retail:crawl-articles:create";
            public const string Update = "retail:crawl-articles:update";
            public const string Delete = "retail:crawl-articles:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticles));
        }

        [DisplayName("CrawlArticleBarcodes")]
        public static class CrawlArticleBarcodes
        {
            public const string View = "retail:crawl-article-barcodes:view";
            public const string Search = "retail:crawl-article-barcodes:search";
            public const string Create = "retail:crawl-article-barcodes:create";
            public const string Update = "retail:crawl-article-barcodes:update";
            public const string Delete = "retail:crawl-article-barcodes:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticleBarcodes));
        }

        [DisplayName("CrawlArticleCategoryRelations")]
        public static class CrawlArticleCategoryRelations
        {
            public const string View = "retail:crawl-article-category-relations:view";
            public const string Search = "retail:crawl-article-category-relations:search";
            public const string Create = "retail:crawl-article-category-relations:create";
            public const string Update = "retail:crawl-article-category-relations:update";
            public const string Delete = "retail:crawl-article-category-relations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticleCategoryRelations));
        }

        [DisplayName("CrawlArticlePrices")]
        public static class CrawlArticlePrices
        {
            public const string View = "retail:crawl-article-prices:view";
            public const string Search = "retail:crawl-article-prices:search";
            public const string Create = "retail:crawl-article-prices:create";
            public const string Update = "retail:crawl-article-prices:update";
            public const string Delete = "retail:crawl-article-prices:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticlePrices));
        }

        [DisplayName("CrawlCategories")]
        public static class CrawlCategories
        {
            public const string View = "retail:crawl-categories:view";
            public const string Search = "retail:crawl-categories:search";
            public const string Create = "retail:crawl-categories:create";
            public const string Update = "retail:crawl-categories:update";
            public const string Delete = "retail:crawl-categories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlCategories));
        }

        [DisplayName("CurrencyExchangeRates")]
        public static class CurrencyExchangeRates
        {
            public const string View = "retail:currency-exchange-rates:view";
            public const string Search = "retail:currency-exchange-rates:search";
            public const string Create = "retail:currency-exchange-rates:create";
            public const string Update = "retail:currency-exchange-rates:update";
            public const string Delete = "retail:currency-exchange-rates:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CurrencyExchangeRates));
        }

        [DisplayName("Customers")]
        public static class Customers
        {
            public const string View = "retail:customers:view";
            public const string Search = "retail:customers:search";
            public const string Create = "retail:customers:create";
            public const string Update = "retail:customers:update";
            public const string Delete = "retail:customers:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Customers));
        }

        [DisplayName("CustomerBalances")]
        public static class CustomerBalances
        {
            public const string View = "retail:customer-balances:view";
            public const string Search = "retail:customer-balances:search";
            public const string Create = "retail:customer-balances:create";
            public const string Update = "retail:customer-balances:update";
            public const string Delete = "retail:customer-balances:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CustomerBalances));
        }

        [DisplayName("CustomerBalanceMovements")]
        public static class CustomerBalanceMovements
        {
            public const string View = "retail:customer-balance-movements:view";
            public const string Search = "retail:customer-balance-movements:search";
            public const string Create = "retail:customer-balance-movements:create";
            public const string Update = "retail:customer-balance-movements:update";
            public const string Delete = "retail:customer-balance-movements:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CustomerBalanceMovements));
        }

        [DisplayName("CustomerLoyaltyCards")]
        public static class CustomerLoyaltyCards
        {
            public const string View = "retail:customer-loyalty-cards:view";
            public const string Search = "retail:customer-loyalty-cards:search";
            public const string Create = "retail:customer-loyalty-cards:create";
            public const string Update = "retail:customer-loyalty-cards:update";
            public const string Delete = "retail:customer-loyalty-cards:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CustomerLoyaltyCards));
        }

        [DisplayName("CustomerLoyaltyTiers")]
        public static class CustomerLoyaltyTiers
        {
            public const string View = "retail:customer-loyalty-tiers:view";
            public const string Search = "retail:customer-loyalty-tiers:search";
            public const string Create = "retail:customer-loyalty-tiers:create";
            public const string Update = "retail:customer-loyalty-tiers:update";
            public const string Delete = "retail:customer-loyalty-tiers:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CustomerLoyaltyTiers));
        }

        [DisplayName("CustomerRequests")]
        public static class CustomerRequests
        {
            public const string View = "retail:customer-requests:view";
            public const string Search = "retail:customer-requests:search";
            public const string Create = "retail:customer-requests:create";
            public const string Update = "retail:customer-requests:update";
            public const string Delete = "retail:customer-requests:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CustomerRequests));
        }

        [DisplayName("CustomerResults")]
        public static class CustomerResults
        {
            public const string View = "retail:customer-results:view";
            public const string Search = "retail:customer-results:search";
            public const string Create = "retail:customer-results:create";
            public const string Update = "retail:customer-results:update";
            public const string Delete = "retail:customer-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CustomerResults));
        }

        [DisplayName("DashboardCardConfigs")]
        public static class DashboardCardConfigs
        {
            public const string View = "retail:dashboard-card-configs:view";
            public const string Search = "retail:dashboard-card-configs:search";
            public const string Create = "retail:dashboard-card-configs:create";
            public const string Update = "retail:dashboard-card-configs:update";
            public const string Delete = "retail:dashboard-card-configs:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(DashboardCardConfigs));
        }

        [DisplayName("EmailAddresses")]
        public static class EmailAddresses
        {
            public const string View = "retail:email-addresses:view";
            public const string Search = "retail:email-addresses:search";
            public const string Create = "retail:email-addresses:create";
            public const string Update = "retail:email-addresses:update";
            public const string Delete = "retail:email-addresses:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(EmailAddresses));
        }

        [DisplayName("Facilities")]
        public static class Facilities
        {
            public const string View = "retail:facilities:view";
            public const string Search = "retail:facilities:search";
            public const string Create = "retail:facilities:create";
            public const string Update = "retail:facilities:update";
            public const string Delete = "retail:facilities:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Facilities));
        }

        [DisplayName("FacilityAddresses")]
        public static class FacilityAddresses
        {
            public const string View = "retail:facility-addresses:view";
            public const string Search = "retail:facility-addresses:search";
            public const string Create = "retail:facility-addresses:create";
            public const string Update = "retail:facility-addresses:update";
            public const string Delete = "retail:facility-addresses:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(FacilityAddresses));
        }

        [DisplayName("FacilityPhones")]
        public static class FacilityPhones
        {
            public const string View = "retail:facility-phones:view";
            public const string Search = "retail:facility-phones:search";
            public const string Create = "retail:facility-phones:create";
            public const string Update = "retail:facility-phones:update";
            public const string Delete = "retail:facility-phones:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(FacilityPhones));
        }

        [DisplayName("InventoryBatches")]
        public static class InventoryBatches
        {
            public const string View = "retail:inventory-batches:view";
            public const string Search = "retail:inventory-batches:search";
            public const string Create = "retail:inventory-batches:create";
            public const string Update = "retail:inventory-batches:update";
            public const string Delete = "retail:inventory-batches:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(InventoryBatches));
        }

        [DisplayName("InventoryBatchMovements")]
        public static class InventoryBatchMovements
        {
            public const string View = "retail:inventory-batch-movements:view";
            public const string Search = "retail:inventory-batch-movements:search";
            public const string Create = "retail:inventory-batch-movements:create";
            public const string Update = "retail:inventory-batch-movements:update";
            public const string Delete = "retail:inventory-batch-movements:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(InventoryBatchMovements));
        }

        [DisplayName("InventoryReservations")]
        public static class InventoryReservations
        {
            public const string View = "retail:inventory-reservations:view";
            public const string Search = "retail:inventory-reservations:search";
            public const string Create = "retail:inventory-reservations:create";
            public const string Update = "retail:inventory-reservations:update";
            public const string Delete = "retail:inventory-reservations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(InventoryReservations));
        }

        [DisplayName("LoyaltyTierConfigurations")]
        public static class LoyaltyTierConfigurations
        {
            public const string View = "retail:loyalty-tier-configurations:view";
            public const string Search = "retail:loyalty-tier-configurations:search";
            public const string Create = "retail:loyalty-tier-configurations:create";
            public const string Update = "retail:loyalty-tier-configurations:update";
            public const string Delete = "retail:loyalty-tier-configurations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(LoyaltyTierConfigurations));
        }

        [DisplayName("NotificationTemplates")]
        public static class NotificationTemplates
        {
            public const string View = "retail:notification-templates:view";
            public const string Search = "retail:notification-templates:search";
            public const string Create = "retail:notification-templates:create";
            public const string Update = "retail:notification-templates:update";
            public const string Delete = "retail:notification-templates:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(NotificationTemplates));
        }

        [DisplayName("Orders")]
        public static class Orders
        {
            public const string View = "retail:orders:view";
            public const string Search = "retail:orders:search";
            public const string Create = "retail:orders:create";
            public const string Update = "retail:orders:update";
            public const string Delete = "retail:orders:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Orders));
        }

        [DisplayName("OrderFulfillments")]
        public static class OrderFulfillments
        {
            public const string View = "retail:order-fulfillments:view";
            public const string Search = "retail:order-fulfillments:search";
            public const string Create = "retail:order-fulfillments:create";
            public const string Update = "retail:order-fulfillments:update";
            public const string Delete = "retail:order-fulfillments:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrderFulfillments));
        }

        [DisplayName("OrderItems")]
        public static class OrderItems
        {
            public const string View = "retail:order-items:view";
            public const string Search = "retail:order-items:search";
            public const string Create = "retail:order-items:create";
            public const string Update = "retail:order-items:update";
            public const string Delete = "retail:order-items:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrderItems));
        }

        [DisplayName("OrderItemFulfillments")]
        public static class OrderItemFulfillments
        {
            public const string View = "retail:order-item-fulfillments:view";
            public const string Search = "retail:order-item-fulfillments:search";
            public const string Create = "retail:order-item-fulfillments:create";
            public const string Update = "retail:order-item-fulfillments:update";
            public const string Delete = "retail:order-item-fulfillments:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrderItemFulfillments));
        }

        [DisplayName("OrderStatusHistories")]
        public static class OrderStatusHistories
        {
            public const string View = "retail:order-status-histories:view";
            public const string Search = "retail:order-status-histories:search";
            public const string Create = "retail:order-status-histories:create";
            public const string Update = "retail:order-status-histories:update";
            public const string Delete = "retail:order-status-histories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrderStatusHistories));
        }

        [DisplayName("OrganisationBrands")]
        public static class OrganisationBrands
        {
            public const string View = "retail:organisation-brands:view";
            public const string Search = "retail:organisation-brands:search";
            public const string Create = "retail:organisation-brands:create";
            public const string Update = "retail:organisation-brands:update";
            public const string Delete = "retail:organisation-brands:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationBrands));
        }

        [DisplayName("OrganisationConfigurations")]
        public static class OrganisationConfigurations
        {
            public const string View = "retail:organisation-configurations:view";
            public const string Search = "retail:organisation-configurations:search";
            public const string Create = "retail:organisation-configurations:create";
            public const string Update = "retail:organisation-configurations:update";
            public const string Delete = "retail:organisation-configurations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationConfigurations));
        }

        [DisplayName("OrganisationEmailAddresses")]
        public static class OrganisationEmailAddresses
        {
            public const string View = "retail:organisation-email-addresses:view";
            public const string Search = "retail:organisation-email-addresses:search";
            public const string Create = "retail:organisation-email-addresses:create";
            public const string Update = "retail:organisation-email-addresses:update";
            public const string Delete = "retail:organisation-email-addresses:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationEmailAddresses));
        }

        [DisplayName("OrganisationFacilities")]
        public static class OrganisationFacilities
        {
            public const string View = "retail:organisation-facilities:view";
            public const string Search = "retail:organisation-facilities:search";
            public const string Create = "retail:organisation-facilities:create";
            public const string Update = "retail:organisation-facilities:update";
            public const string Delete = "retail:organisation-facilities:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationFacilities));
        }

        [DisplayName("OrganisationOnboardingProgress")]
        public static class OrganisationOnboardingProgress
        {
            public const string View = "retail:organisation-onboarding-progress:view";
            public const string Search = "retail:organisation-onboarding-progress:search";
            public const string Create = "retail:organisation-onboarding-progress:create";
            public const string Update = "retail:organisation-onboarding-progress:update";
            public const string Delete = "retail:organisation-onboarding-progress:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationOnboardingProgress));
        }

        [DisplayName("OrganisationPartnershipConfigurations")]
        public static class OrganisationPartnershipConfigurations
        {
            public const string View = "retail:organisation-partnership-configurations:view";
            public const string Search = "retail:organisation-partnership-configurations:search";
            public const string Create = "retail:organisation-partnership-configurations:create";
            public const string Update = "retail:organisation-partnership-configurations:update";
            public const string Delete = "retail:organisation-partnership-configurations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationPartnershipConfigurations));
        }

        [DisplayName("OrganisationRelationships")]
        public static class OrganisationRelationships
        {
            public const string View = "retail:organisation-relationships:view";
            public const string Search = "retail:organisation-relationships:search";
            public const string Create = "retail:organisation-relationships:create";
            public const string Update = "retail:organisation-relationships:update";
            public const string Delete = "retail:organisation-relationships:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationRelationships));
        }

        [DisplayName("PartnershipResponsibleUsers")]
        public static class PartnershipResponsibleUsers
        {
            public const string View = "retail:partnership-responsible-users:view";
            public const string Search = "retail:partnership-responsible-users:search";
            public const string Create = "retail:partnership-responsible-users:create";
            public const string Update = "retail:partnership-responsible-users:update";
            public const string Delete = "retail:partnership-responsible-users:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PartnershipResponsibleUsers));
        }

        [DisplayName("Payments")]
        public static class Payments
        {
            public const string View = "retail:payments:view";
            public const string Search = "retail:payments:search";
            public const string Create = "retail:payments:create";
            public const string Update = "retail:payments:update";
            public const string Delete = "retail:payments:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Payments));
        }

        [DisplayName("PaymentIntentRequests")]
        public static class PaymentIntentRequests
        {
            public const string View = "retail:payment-intent-requests:view";
            public const string Search = "retail:payment-intent-requests:search";
            public const string Create = "retail:payment-intent-requests:create";
            public const string Update = "retail:payment-intent-requests:update";
            public const string Delete = "retail:payment-intent-requests:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentIntentRequests));
        }

        [DisplayName("PaymentIntentResults")]
        public static class PaymentIntentResults
        {
            public const string View = "retail:payment-intent-results:view";
            public const string Search = "retail:payment-intent-results:search";
            public const string Create = "retail:payment-intent-results:create";
            public const string Update = "retail:payment-intent-results:update";
            public const string Delete = "retail:payment-intent-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentIntentResults));
        }

        [DisplayName("PaymentMethodRequests")]
        public static class PaymentMethodRequests
        {
            public const string View = "retail:payment-method-requests:view";
            public const string Search = "retail:payment-method-requests:search";
            public const string Create = "retail:payment-method-requests:create";
            public const string Update = "retail:payment-method-requests:update";
            public const string Delete = "retail:payment-method-requests:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentMethodRequests));
        }

        [DisplayName("PaymentMethodResults")]
        public static class PaymentMethodResults
        {
            public const string View = "retail:payment-method-results:view";
            public const string Search = "retail:payment-method-results:search";
            public const string Create = "retail:payment-method-results:create";
            public const string Update = "retail:payment-method-results:update";
            public const string Delete = "retail:payment-method-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentMethodResults));
        }

        [DisplayName("PaymentProviders")]
        public static class PaymentProviders
        {
            public const string View = "retail:payment-providers:view";
            public const string Search = "retail:payment-providers:search";
            public const string Create = "retail:payment-providers:create";
            public const string Update = "retail:payment-providers:update";
            public const string Delete = "retail:payment-providers:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentProviders));
        }

        [DisplayName("PaymentProviderConfigurations")]
        public static class PaymentProviderConfigurations
        {
            public const string View = "retail:payment-provider-configurations:view";
            public const string Search = "retail:payment-provider-configurations:search";
            public const string Create = "retail:payment-provider-configurations:create";
            public const string Update = "retail:payment-provider-configurations:update";
            public const string Delete = "retail:payment-provider-configurations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentProviderConfigurations));
        }

        [DisplayName("PaymentRequests")]
        public static class PaymentRequests
        {
            public const string View = "retail:payment-requests:view";
            public const string Search = "retail:payment-requests:search";
            public const string Create = "retail:payment-requests:create";
            public const string Update = "retail:payment-requests:update";
            public const string Delete = "retail:payment-requests:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentRequests));
        }

        [DisplayName("PaymentResults")]
        public static class PaymentResults
        {
            public const string View = "retail:payment-results:view";
            public const string Search = "retail:payment-results:search";
            public const string Create = "retail:payment-results:create";
            public const string Update = "retail:payment-results:update";
            public const string Delete = "retail:payment-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentResults));
        }

        [DisplayName("PaymentStatusResults")]
        public static class PaymentStatusResults
        {
            public const string View = "retail:payment-status-results:view";
            public const string Search = "retail:payment-status-results:search";
            public const string Create = "retail:payment-status-results:create";
            public const string Update = "retail:payment-status-results:update";
            public const string Delete = "retail:payment-status-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentStatusResults));
        }

        [DisplayName("PaymentTransactions")]
        public static class PaymentTransactions
        {
            public const string View = "retail:payment-transactions:view";
            public const string Search = "retail:payment-transactions:search";
            public const string Create = "retail:payment-transactions:create";
            public const string Update = "retail:payment-transactions:update";
            public const string Delete = "retail:payment-transactions:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PaymentTransactions));
        }

        [DisplayName("Phones")]
        public static class Phones
        {
            public const string View = "retail:phones:view";
            public const string Search = "retail:phones:search";
            public const string Create = "retail:phones:create";
            public const string Update = "retail:phones:update";
            public const string Delete = "retail:phones:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Phones));
        }

        [DisplayName("PriceHistory")]
        public static class PriceHistory
        {
            public const string View = "retail:price-history:view";
            public const string Search = "retail:price-history:search";
            public const string Create = "retail:price-history:create";
            public const string Update = "retail:price-history:update";
            public const string Delete = "retail:price-history:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PriceHistory));
        }

        [DisplayName("PricingAuditLogs")]
        public static class PricingAuditLogs
        {
            public const string View = "retail:pricing-audit-logs:view";
            public const string Search = "retail:pricing-audit-logs:search";
            public const string Create = "retail:pricing-audit-logs:create";
            public const string Update = "retail:pricing-audit-logs:update";
            public const string Delete = "retail:pricing-audit-logs:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PricingAuditLogs));
        }

        [DisplayName("Products")]
        public static class Products
        {
            public const string View = "retail:products:view";
            public const string Search = "retail:products:search";
            public const string Create = "retail:products:create";
            public const string Update = "retail:products:update";
            public const string Delete = "retail:products:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Products));
        }

        [DisplayName("ProductAttributes")]
        public static class ProductAttributes
        {
            public const string View = "retail:product-attributes:view";
            public const string Search = "retail:product-attributes:search";
            public const string Create = "retail:product-attributes:create";
            public const string Update = "retail:product-attributes:update";
            public const string Delete = "retail:product-attributes:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductAttributes));
        }

        [DisplayName("ProductAttributeValues")]
        public static class ProductAttributeValues
        {
            public const string View = "retail:product-attribute-values:view";
            public const string Search = "retail:product-attribute-values:search";
            public const string Create = "retail:product-attribute-values:create";
            public const string Update = "retail:product-attribute-values:update";
            public const string Delete = "retail:product-attribute-values:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductAttributeValues));
        }

        [DisplayName("ProductCategoryRelations")]
        public static class ProductCategoryRelations
        {
            public const string View = "retail:product-category-relations:view";
            public const string Search = "retail:product-category-relations:search";
            public const string Create = "retail:product-category-relations:create";
            public const string Update = "retail:product-category-relations:update";
            public const string Delete = "retail:product-category-relations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductCategoryRelations));
        }

        [DisplayName("ProductVariantDiscounts")]
        public static class ProductVariantDiscounts
        {
            public const string View = "retail:product-variant-discounts:view";
            public const string Search = "retail:product-variant-discounts:search";
            public const string Create = "retail:product-variant-discounts:create";
            public const string Update = "retail:product-variant-discounts:update";
            public const string Delete = "retail:product-variant-discounts:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductVariantDiscounts));
        }

        [DisplayName("ProductExcelConfigurations")]
        public static class ProductExcelConfigurations
        {
            public const string View = "retail:product-excel-configurations:view";
            public const string Search = "retail:product-excel-configurations:search";
            public const string Create = "retail:product-excel-configurations:create";
            public const string Update = "retail:product-excel-configurations:update";
            public const string Delete = "retail:product-excel-configurations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductExcelConfigurations));
        }

        [DisplayName("ProductExportConfigurations")]
        public static class ProductExportConfigurations
        {
            public const string View = "retail:product-export-configurations:view";
            public const string Search = "retail:product-export-configurations:search";
            public const string Create = "retail:product-export-configurations:create";
            public const string Update = "retail:product-export-configurations:update";
            public const string Delete = "retail:product-export-configurations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductExportConfigurations));
        }

        [DisplayName("ProductImages")]
        public static class ProductImages
        {
            public const string View = "retail:product-images:view";
            public const string Search = "retail:product-images:search";
            public const string Create = "retail:product-images:create";
            public const string Update = "retail:product-images:update";
            public const string Delete = "retail:product-images:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductImages));
        }

        [DisplayName("ProductInventories")]
        public static class ProductInventories
        {
            public const string View = "retail:product-inventories:view";
            public const string Search = "retail:product-inventories:search";
            public const string Create = "retail:product-inventories:create";
            public const string Update = "retail:product-inventories:update";
            public const string Delete = "retail:product-inventories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductInventories));
        }

        [DisplayName("ProductProfitMargins")]
        public static class ProductProfitMargins
        {
            public const string View = "retail:product-profit-margins:view";
            public const string Search = "retail:product-profit-margins:search";
            public const string Create = "retail:product-profit-margins:create";
            public const string Update = "retail:product-profit-margins:update";
            public const string Delete = "retail:product-profit-margins:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductProfitMargins));
        }

        [DisplayName("ProductProposals")]
        public static class ProductProposals
        {
            public const string View = "retail:product-proposals:view";
            public const string Search = "retail:product-proposals:search";
            public const string Create = "retail:product-proposals:create";
            public const string Update = "retail:product-proposals:update";
            public const string Delete = "retail:product-proposals:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductProposals));
        }

        [DisplayName("ProductSuppliers")]
        public static class ProductSuppliers
        {
            public const string View = "retail:product-suppliers:view";
            public const string Search = "retail:product-suppliers:search";
            public const string Create = "retail:product-suppliers:create";
            public const string Update = "retail:product-suppliers:update";
            public const string Delete = "retail:product-suppliers:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductSuppliers));
        }

        [DisplayName("ProductSupplierDiscounts")]
        public static class ProductSupplierDiscounts
        {
            public const string View = "retail:product-supplier-discounts:view";
            public const string Search = "retail:product-supplier-discounts:search";
            public const string Create = "retail:product-supplier-discounts:create";
            public const string Update = "retail:product-supplier-discounts:update";
            public const string Delete = "retail:product-supplier-discounts:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductSupplierDiscounts));
        }

        [DisplayName("ProductVariants")]
        public static class ProductVariants
        {
            public const string View = "retail:product-variants:view";
            public const string Search = "retail:product-variants:search";
            public const string Create = "retail:product-variants:create";
            public const string Update = "retail:product-variants:update";
            public const string Delete = "retail:product-variants:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductVariants));
        }

        [DisplayName("ProductVariantAttributes")]
        public static class ProductVariantAttributes
        {
            public const string View = "retail:product-variant-attributes:view";
            public const string Search = "retail:product-variant-attributes:search";
            public const string Create = "retail:product-variant-attributes:create";
            public const string Update = "retail:product-variant-attributes:update";
            public const string Delete = "retail:product-variant-attributes:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductVariantAttributes));
        }

        [DisplayName("ProductVariantImages")]
        public static class ProductVariantImages
        {
            public const string View = "retail:product-variant-images:view";
            public const string Search = "retail:product-variant-images:search";
            public const string Create = "retail:product-variant-images:create";
            public const string Update = "retail:product-variant-images:update";
            public const string Delete = "retail:product-variant-images:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductVariantImages));
        }

        [DisplayName("ProductDocuments")]
        public static class ProductDocuments
        {
            public const string View = "retail:product-documents:view";
            public const string Search = "retail:product-documents:search";
            public const string Create = "retail:product-documents:create";
            public const string Update = "retail:product-documents:update";
            public const string Delete = "retail:product-documents:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductDocuments));
        }

        [DisplayName("InventoryMovements")]
        public static class InventoryMovements
        {
            public const string View = "retail:inventory-movements:view";
            public const string Search = "retail:inventory-movements:search";
            public const string Create = "retail:inventory-movements:create";
            public const string Update = "retail:inventory-movements:update";
            public const string Delete = "retail:inventory-movements:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(InventoryMovements));
        }

        [DisplayName("Inventories")]
        public static class Inventories
        {
            public const string View = "retail:inventories:view";
            public const string Search = "retail:inventories:search";
            public const string Create = "retail:inventories:create";
            public const string Update = "retail:inventories:update";
            public const string Delete = "retail:inventories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Inventories));
        }

        [DisplayName("ProductVariantPriceHistories")]
        public static class ProductVariantPriceHistories
        {
            public const string View = "retail:product-variant-price-histories:view";
            public const string Search = "retail:product-variant-price-histories:search";
            public const string Create = "retail:product-variant-price-histories:create";
            public const string Update = "retail:product-variant-price-histories:update";
            public const string Delete = "retail:product-variant-price-histories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductVariantPriceHistories));
        }

        [DisplayName("PromotionCampaigns")]
        public static class PromotionCampaigns
        {
            public const string View = "retail:promotion-campaigns:view";
            public const string Search = "retail:promotion-campaigns:search";
            public const string Create = "retail:promotion-campaigns:create";
            public const string Update = "retail:promotion-campaigns:update";
            public const string Delete = "retail:promotion-campaigns:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PromotionCampaigns));
        }

        [DisplayName("PromotionCampaignRegistrations")]
        public static class PromotionCampaignRegistrations
        {
            public const string View = "retail:promotion-campaign-registrations:view";
            public const string Search = "retail:promotion-campaign-registrations:search";
            public const string Create = "retail:promotion-campaign-registrations:create";
            public const string Update = "retail:promotion-campaign-registrations:update";
            public const string Delete = "retail:promotion-campaign-registrations:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PromotionCampaignRegistrations));
        }

        [DisplayName("PromotionCampaignOffers")]
        public static class PromotionCampaignOffers
        {
            public const string View = "retail:promotion-campaign-offers:view";
            public const string Search = "retail:promotion-campaign-offers:search";
            public const string Create = "retail:promotion-campaign-offers:create";
            public const string Update = "retail:promotion-campaign-offers:update";
            public const string Delete = "retail:promotion-campaign-offers:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(PromotionCampaignOffers));
        }

        [DisplayName("RefundRequests")]
        public static class RefundRequests
        {
            public const string View = "retail:refund-requests:view";
            public const string Search = "retail:refund-requests:search";
            public const string Create = "retail:refund-requests:create";
            public const string Update = "retail:refund-requests:update";
            public const string Delete = "retail:refund-requests:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(RefundRequests));
        }

        [DisplayName("RefundResults")]
        public static class RefundResults
        {
            public const string View = "retail:refund-results:view";
            public const string Search = "retail:refund-results:search";
            public const string Create = "retail:refund-results:create";
            public const string Update = "retail:refund-results:update";
            public const string Delete = "retail:refund-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(RefundResults));
        }

        [DisplayName("Regions")]
        public static class Regions
        {
            public const string View = "retail:regions:view";
            public const string Search = "retail:regions:search";
            public const string Create = "retail:regions:create";
            public const string Update = "retail:regions:update";
            public const string Delete = "retail:regions:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Regions));
        }

        [DisplayName("SalesFacilities")]
        public static class SalesFacilities
        {
            public const string View = "retail:sales-facilities:view";
            public const string Search = "retail:sales-facilities:search";
            public const string Create = "retail:sales-facilities:create";
            public const string Update = "retail:sales-facilities:update";
            public const string Delete = "retail:sales-facilities:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(SalesFacilities));
        }

        [DisplayName("ShippingInfos")]
        public static class ShippingInfos
        {
            public const string View = "retail:shipping-infos:view";
            public const string Search = "retail:shipping-infos:search";
            public const string Create = "retail:shipping-infos:create";
            public const string Update = "retail:shipping-infos:update";
            public const string Delete = "retail:shipping-infos:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ShippingInfos));
        }

        [DisplayName("StickerDiscounts")]
        public static class StickerDiscounts
        {
            public const string View = "retail:sticker-discounts:view";
            public const string Search = "retail:sticker-discounts:search";
            public const string Create = "retail:sticker-discounts:create";
            public const string Update = "retail:sticker-discounts:update";
            public const string Delete = "retail:sticker-discounts:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(StickerDiscounts));
        }

        [DisplayName("SubscriptionRequests")]
        public static class SubscriptionRequests
        {
            public const string View = "retail:subscription-requests:view";
            public const string Search = "retail:subscription-requests:search";
            public const string Create = "retail:subscription-requests:create";
            public const string Update = "retail:subscription-requests:update";
            public const string Delete = "retail:subscription-requests:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(SubscriptionRequests));
        }

        [DisplayName("SubscriptionResults")]
        public static class SubscriptionResults
        {
            public const string View = "retail:subscription-results:view";
            public const string Search = "retail:subscription-results:search";
            public const string Create = "retail:subscription-results:create";
            public const string Update = "retail:subscription-results:update";
            public const string Delete = "retail:subscription-results:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(SubscriptionResults));
        }

        [DisplayName("TaxCategories")]
        public static class TaxCategories
        {
            public const string View = "retail:tax-categories:view";
            public const string Search = "retail:tax-categories:search";
            public const string Create = "retail:tax-categories:create";
            public const string Update = "retail:tax-categories:update";
            public const string Delete = "retail:tax-categories:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(TaxCategories));
        }

        [DisplayName("Terminals")]
        public static class Terminals
        {
            public const string View = "retail:terminals:view";
            public const string Search = "retail:terminals:search";
            public const string Create = "retail:terminals:create";
            public const string Update = "retail:terminals:update";
            public const string Delete = "retail:terminals:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Terminals));
        }

        [DisplayName("UnitOfMeasures")]
        public static class UnitOfMeasures
        {
            public const string View = "retail:unit-of-measures:view";
            public const string Search = "retail:unit-of-measures:search";
            public const string Create = "retail:unit-of-measures:create";
            public const string Update = "retail:unit-of-measures:update";
            public const string Delete = "retail:unit-of-measures:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(UnitOfMeasures));
        }

        [DisplayName("Warehouses")]
        public static class Warehouses
        {
            public const string View = "retail:warehouses:view";
            public const string Search = "retail:warehouses:search";
            public const string Create = "retail:warehouses:create";
            public const string Update = "retail:warehouses:update";
            public const string Delete = "retail:warehouses:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Warehouses));
        }

        [DisplayName("Hangfire")]
        public static class Hangfire
        {
            public const string View = "retail:hangfire:view";
            public const string Search = "retail:hangfire:search";
            public const string Create = "retail:hangfire:create";
            public const string Update = "retail:hangfire:update";
            public const string Delete = "retail:hangfire:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Hangfire));
        }

        [DisplayName("Suppliers")]
        public static class Suppliers
        {
            public const string View = "retail:suppliers:view";
            public const string Search = "retail:suppliers:search";
            public const string Create = "retail:suppliers:create";
            public const string Update = "retail:suppliers:update";
            public const string Delete = "retail:suppliers:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Suppliers));
        }

        [DisplayName("Vendors")]
        public static class Vendors
        {
            public const string View = "retail:vendors:view";
            public const string Search = "retail:vendors:search";
            public const string Create = "retail:vendors:create";
            public const string Update = "retail:vendors:update";
            public const string Delete = "retail:vendors:delete";
            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Vendors));
        }

        private static readonly ConcurrentDictionary<string, HashSet<string>> _modulePermissionsCache = new();

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return Regex.Replace(value, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
        }

        public static HashSet<string> GeneratePermissionsForModule(string module) =>
            _modulePermissionsCache.GetOrAdd(ToKebabCase(module), key =>
            [
                $"retail:{key}:create",
                $"retail:{key}:view",
                $"retail:{key}:update",
                $"retail:{key}:delete",
                $"retail:{key}:search"
            ]);
    }

    public static class TMS
    {
        public static class Task
        {
            public const string Create = "tms:task:create";
            public const string Read = "tms:task:read";
            public const string Update = "tms:task:update";
            public const string Delete = "tms:task:delete";
        }
    }

    public static class HRMS
    {
        public static class Profile
        {
            public const string Read = "hrms:profile:read";
        }

        public static class Employee
        {
            public const string Create = "hrms:employee:create";
            public const string Read = "hrms:employee:read";
            public const string Update = "hrms:employee:update";
            public const string Delete = "hrms:employee:delete";
            public const string AddAddress = "hrms:employee:add-address";
        }

        public static class Department
        {
            public const string Create = "hrms:department:create";
            public const string Read = "hrms:department:read";
            public const string Update = "hrms:department:update";
            public const string Delete = "hrms:department:delete";
        }

        public static class Position
        {
            public const string Create = "hrms:position:create";
            public const string Read = "hrms:position:read";
            public const string Update = "hrms:position:update";
            public const string Delete = "hrms:position:delete";
        }

        public static class Location
        {
            public const string Create = "hrms:location:create";
            public const string Read = "hrms:location:read";
            public const string Update = "hrms:location:update";
            public const string Delete = "hrms:location:delete";
        }

        public static class EmployeeAddress
        {
            public const string Create = "hrms:employee-address:create";
            public const string Read = "hrms:employee-address:read";
            public const string Update = "hrms:employee-address:update";
            public const string Delete = "hrms:employee-address:delete";
        }

        public static class EmployeeBankInformation
        {
            public const string Create = "hrms:employee-bank-information:create";
            public const string Read = "hrms:employee-bank-information:read";
            public const string Update = "hrms:employee-bank-information:update";
            public const string Delete = "hrms:employee-bank-information:delete";
        }

        public static class EmployeeEducation
        {
            public const string Create = "hrms:employee-education:create";
            public const string Read = "hrms:employee-education:read";
            public const string Update = "hrms:employee-education:update";
            public const string Delete = "hrms:employee-education:delete";
        }

        public static class EmployeeEmail
        {
            public const string Create = "hrms:employee-email:create";
            public const string Read = "hrms:employee-email:read";
            public const string Update = "hrms:employee-email:update";
            public const string Delete = "hrms:employee-email:delete";
        }

        public static class EmergencyContact
        {
            public const string Create = "hrms:employee-emergency-contact:create";
            public const string Read = "hrms:employee-emergency-contact:read";
            public const string Update = "hrms:employee-emergency-contact:update";
            public const string Delete = "hrms:employee-emergency-contact:delete";
        }

        public static class EmployeeLeave
        {
            public const string Create = "hrms:employee-leave:create";
            public const string Read = "hrms:employee-leave:read";
            public const string Update = "hrms:employee-leave:update";
            public const string Delete = "hrms:employee-leave:delete";
            public const string Reset = "hrms:employee-leave:reset";
        }

        public static class EmployeeSalaryHistory
        {
            public const string Create = "hrms:employee-salary-history:create";
            public const string Read = "hrms:employee-salary-history:read";
            public const string Update = "hrms:employee-salary-history:update";
            public const string Delete = "hrms:employee-salary-history:delete";
        }

        public static class Shift
        {
            public const string Create = "hrms:shift:create";
            public const string Read = "hrms:shift:read";
            public const string Update = "hrms:shift:update";
            public const string Delete = "hrms:shift:delete";
        }

        public static class Dashboard
        {
            public const string Read = "hrms:dashboard:read";
        }
    }

    /// <summary>
    /// Nexus Identity system permissions
    /// These permissions control access to the identity platform itself
    /// </summary>
    public static class Nexus
    {
        public static class Users
        {
            public const string View = "nexus:users:view";
            public const string Create = "nexus:users:create";
            public const string Update = "nexus:users:update";
            public const string Delete = "nexus:users:delete";
        }

        public static class Organisations
        {
            public const string View = "nexus:organisations:view";
            public const string Create = "nexus:organisations:create";
            public const string Update = "nexus:organisations:update";
            public const string Delete = "nexus:organisations:delete";
        }

        public static class Roles
        {
            public const string View = "nexus:roles:view";
            public const string Create = "nexus:roles:create";
            public const string Update = "nexus:roles:update";
            public const string Delete = "nexus:roles:delete";
        }

        public static class Permissions
        {
            public const string View = "nexus:permissions:view";
            public const string Create = "nexus:permissions:create";
            public const string Update = "nexus:permissions:update";
            public const string Delete = "nexus:permissions:delete";
        }

        public static class UserClaims
        {
            public const string View = "nexus:user-claims:view";
            public const string Create = "nexus:user-claims:create";
            public const string Update = "nexus:user-claims:update";
            public const string Delete = "nexus:user-claims:delete";
        }

        public static class Admin
        {
            public const string ViewAll = "nexus:admin:view-all";
            public const string ManageSystem = "nexus:admin:system";
            public const string AccessAdminPanel = "nexus:admin:panel";
        }

        public static class Currencies
        {
            public const string View = "nexus:currencies:view";
            public const string Create = "nexus:currencies:create";
            public const string Update = "nexus:currencies:update";
            public const string Delete = "nexus:currencies:delete";
        }
    }
}
