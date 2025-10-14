using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

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
        [DisplayName("Brands")]
        [Description("Brands Permissions")]
        public static class Brands
        {
            public const string View = "retail:brands:view";
            public const string Search = "retail:brands:search";
            public const string Create = "retail:brands:create";
            public const string Update = "retail:brands:update";
            public const string Delete = "retail:brands:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Brands));
        }

        [DisplayName("Leaflets")]
        public static class Leaflets
        {
            public const string View = "retail:leaflets:view";
            public const string Search = "retail:leaflets:search";
            public const string Create = "retail:leaflets:create";
            public const string Update = "retail:leaflets:update";
            public const string Delete = "retail:leaflets:delete";
        }

        [DisplayName("LeafletDiscount")]
        [Description("LeafletDiscount Permissions")]
        public static class LeafletDiscountClaims
        {
            public const string View = "retail:leaflet-discount:view";
            public const string Search = "retail:leaflet-discount:search";
            public const string Create = "retail:leaflet-discount:create";
            public const string Update = "retail:leaflet-discount:update";
            public const string Delete = "retail:leaflet-discount:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(LeafletDiscountClaims));
        }

        [DisplayName("CrawlArticleBarcode")]
        [Description("CrawlArticleBarcode Permissions")]
        public static class CrawlArticleBarcode
        {
            public const string View = "retail:crawl-article-barcode:view";
            public const string Search = "retail:crawl-article-barcode:search";
            public const string Create = "retail:crawl-article-barcode:create";
            public const string Update = "retail:crawl-article-barcode:update";
            public const string Delete = "retail:crawl-article-barcode:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticleBarcode));
        }

        [DisplayName("CrawlArticlePrice")]
        [Description("CrawlArticlePrice Permissions")]
        public static class CrawlArticlePrice
        {
            public const string View = "retail:crawl-article-price:view";
            public const string Search = "retail:crawl-article-price:search";
            public const string Create = "retail:crawl-article-price:create";
            public const string Update = "retail:crawl-article-price:update";
            public const string Delete = "retail:crawl-article-price:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticlePrice));
        }

        [DisplayName("Crawls")]
        [Description("Crawls Permissions")]
        public static class Crawls
        {
            public const string View = "retail:crawls:view";
            public const string Search = "retail:crawls:search";
            public const string Create = "retail:crawls:create";
            public const string Update = "retail:crawls:update";
            public const string Delete = "retail:crawls:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Crawls));
        }

        [DisplayName("Exceptions")]
        [Description("Exceptions Permissions")]
        public static class Exceptions
        {
            public const string View = "retail:exceptions:view";
            public const string Search = "retail:exceptions:search";
            public const string Create = "retail:exceptions:create";
            public const string Update = "retail:exceptions:update";
            public const string Delete = "retail:exceptions:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Exceptions));
        }

        [DisplayName("Hangfire")]
        [Description("Hangfire Permissions")]
        public static class Hangfire
        {
            public const string View = "retail:hangfire:view";
            public const string Search = "retail:hangfire:search";
            public const string Create = "retail:hangfire:create";
            public const string Update = "retail:hangfire:update";
            public const string Delete = "retail:hangfire:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Hangfire));
        }

        [DisplayName("Images")]
        [Description("Images Permissions")]
        public static class Images
        {
            public const string View = "retail:images:view";
            public const string Search = "retail:images:search";
            public const string Create = "retail:images:create";
            public const string Update = "retail:images:update";
            public const string Delete = "retail:images:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Images));
        }

        [DisplayName("Manufacturers")]
        [Description("Manufacturers Permissions")]
        public static class Manufacturers
        {
            public const string View = "retail:manufacturers:view";
            public const string Search = "retail:manufacturers:search";
            public const string Create = "retail:manufacturers:create";
            public const string Update = "retail:manufacturers:update";
            public const string Delete = "retail:manufacturers:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Manufacturers));
        }

        [DisplayName("ProductSuppliers")]
        [Description("ProductSuppliers Permissions")]
        public static class ProductSuppliers
        {
            public const string View = "retail:product-suppliers:view";
            public const string Search = "retail:product-suppliers:search";
            public const string Create = "retail:product-suppliers:create";
            public const string Update = "retail:product-suppliers:update";
            public const string Delete = "retail:product-suppliers:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductSuppliers));
        }

        [DisplayName("Providers")]
        [Description("Providers Permissions")]
        public static class Providers
        {
            public const string View = "retail:providers:view";
            public const string Search = "retail:providers:search";
            public const string Create = "retail:providers:create";
            public const string Update = "retail:providers:update";
            public const string Delete = "retail:providers:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Providers));
        }

        [DisplayName("Stores")]
        [Description("Stores Permissions")]
        public static class Stores
        {
            public const string View = "retail:stores:view";
            public const string Search = "retail:stores:search";
            public const string Create = "retail:stores:create";
            public const string Update = "retail:stores:update";
            public const string Delete = "retail:stores:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Stores));
        }

        [DisplayName("ShoppingCart")]
        [Description("ShoppingCart Permissions")]
        public static class ShoppingCart
        {
            public const string View = "retail:shopping-cart:view";
            public const string Create = "retail:shopping-cart:create";
            public const string Update = "retail:shopping-cart:update";
            public const string Delete = "retail:shopping-cart:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ShoppingCart));
        }

        [DisplayName("TaxCategory")]
        [Description("TaxCategory Permissions")]
        public static class TaxCategory
        {
            public const string View = "retail:tax-category:view";
            public const string Search = "retail:tax-category:search";
            public const string Create = "retail:tax-category:create";
            public const string Update = "retail:tax-category:update";
            public const string Delete = "retail:tax-category:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(TaxCategory));
        }

        [DisplayName("UnitOfMeasure")]
        [Description("UnitOfMeasure Permissions")]
        public static class UnitOfMeasure
        {
            public const string View = "retail:unit-of-measure:view";
            public const string Search = "retail:unit-of-measure:search";
            public const string Create = "retail:unit-of-measure:create";
            public const string Update = "retail:unit-of-measure:update";
            public const string Delete = "retail:unit-of-measure:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(UnitOfMeasure));
        }

        [DisplayName("Warehouses")]
        [Description("Warehouses Permissions")]
        public static class Warehouses
        {
            public const string View = "retail:warehouses:view";
            public const string Search = "retail:warehouses:search";
            public const string Create = "retail:warehouses:create";
            public const string Update = "retail:warehouses:update";
            public const string Delete = "retail:warehouses:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Warehouses));
        }

        [DisplayName("AppLog")]
        [Description("AppLog Permissions")]
        public static class AppLog
        {
            public const string View = "retail:app-log:view";
            public const string Search = "retail:app-log:search";
            public const string Create = "retail:app-log:create";
            public const string Update = "retail:app-log:update";
            public const string Delete = "retail:app-log:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(AppLog));
        }

        [DisplayName("BaseProduct")]
        [Description("BaseProduct Permissions")]
        public static class BaseProduct
        {
            public const string View = "retail:base-product:view";
            public const string Search = "retail:base-product:search";
            public const string Create = "retail:base-product:create";
            public const string Update = "retail:base-product:update";
            public const string Delete = "retail:base-product:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(BaseProduct));
        }

        [DisplayName("CrawlArticle")]
        [Description("CrawlArticle Permissions")]
        public static class CrawlArticle
        {
            public const string View = "retail:crawl-article:view";
            public const string Search = "retail:crawl-article:search";
            public const string Create = "retail:crawl-article:create";
            public const string Update = "retail:crawl-article:update";
            public const string Delete = "retail:crawl-article:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(CrawlArticle));
        }

        [DisplayName("Customers")]
        [Description("Customers Permissions")]
        public static class Customers
        {
            public const string View = "retail:customers:view";
            public const string Search = "retail:customers:search";
            public const string Create = "retail:customers:create";
            public const string Update = "retail:customers:update";
            public const string Delete = "retail:customers:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Customers));
        }

        [DisplayName("Facilities")]
        [Description("Facilities Permissions")]
        public static class Facilities
        {
            public const string View = "retail:facilities:view";
            public const string Search = "retail:facilities:search";
            public const string Create = "retail:facilities:create";
            public const string Update = "retail:facilities:update";
            public const string Delete = "retail:facilities:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Facilities));
        }

        [DisplayName("Orders")]
        [Description("Orders Permissions")]
        public static class Orders
        {
            public const string View = "retail:orders:view";
            public const string Search = "retail:orders:search";
            public const string Create = "retail:orders:create";
            public const string Update = "retail:orders:update";
            public const string Delete = "retail:orders:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Orders));
        }

        [DisplayName("OrganisationConfiguration")]
        [Description("OrganisationConfiguration Permissions")]
        public static class OrganisationConfiguration
        {
            public const string View = "retail:organisation-configuration:view";
            public const string Search = "retail:organisation-configuration:search";
            public const string Create = "retail:organisation-configuration:create";
            public const string Update = "retail:organisation-configuration:update";
            public const string Delete = "retail:organisation-configuration:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationConfiguration));
        }

        [DisplayName("OrganisationOnboardingProgress")]
        [Description("OrganisationOnboardingProgress Permissions")]
        public static class OrganisationOnboardingProgress
        {
            public const string View = "retail:organisation-onboarding-progress:view";
            public const string Search = "retail:organisation-onboarding-progress:search";
            public const string Create = "retail:organisation-onboarding-progress:create";
            public const string Update = "retail:organisation-onboarding-progress:update";
            public const string Delete = "retail:organisation-onboarding-progress:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(OrganisationOnboardingProgress));
        }

        [DisplayName("Pos")]
        [Description("Pos Permissions")]
        public static class Pos
        {
            public const string View = "retail:pos:view";
            public const string Search = "retail:pos:search";
            public const string Create = "retail:pos:create";
            public const string Update = "retail:pos:update";
            public const string Delete = "retail:pos:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Pos));
        }

        [DisplayName("ProductDiscount")]
        [Description("ProductDiscount Permissions")]
        public static class ProductDiscount
        {
            public const string View = "retail:product-discount:view";
            public const string Search = "retail:product-discount:search";
            public const string Create = "retail:product-discount:create";
            public const string Update = "retail:product-discount:update";
            public const string Delete = "retail:product-discount:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductDiscount));
        }

        [DisplayName("ProductImage")]
        [Description("ProductImage Permissions")]
        public static class ProductImage
        {
            public const string View = "retail:product-image:view";
            public const string Search = "retail:product-image:search";
            public const string Create = "retail:product-image:create";
            public const string Update = "retail:product-image:update";
            public const string Delete = "retail:product-image:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductImage));
        }

        [DisplayName("Reports")]
        [Description("Reports Permissions")]
        public static class Reports
        {
            public const string View = "retail:reports:view";
            public const string Search = "retail:reports:search";
            public const string Create = "retail:reports:create";
            public const string Update = "retail:reports:update";
            public const string Delete = "retail:reports:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Reports));
        }

        [DisplayName("StickerDiscounts")]
        [Description("StickerDiscounts Permissions")]
        public static class StickerDiscounts
        {
            public const string View = "retail:sticker-discounts:view";
            public const string Search = "retail:sticker-discounts:search";
            public const string Create = "retail:sticker-discounts:create";
            public const string Update = "retail:sticker-discounts:update";
            public const string Delete = "retail:sticker-discounts:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(StickerDiscounts));
        }

        [DisplayName("Suppliers")]
        [Description("Suppliers Permissions")]
        public static class Suppliers
        {
            public const string View = "retail:suppliers:view";
            public const string Search = "retail:suppliers:search";
            public const string Create = "retail:suppliers:create";
            public const string Update = "retail:suppliers:update";
            public const string Delete = "retail:suppliers:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Suppliers));
        }

        [DisplayName("Transactions")]
        [Description("Transactions Permissions")]
        public static class Transactions
        {
            public const string View = "retail:transactions:view";
            public const string Search = "retail:transactions:search";
            public const string Create = "retail:transactions:create";
            public const string Update = "retail:transactions:update";
            public const string Delete = "retail:transactions:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Transactions));
        }

        [DisplayName("Vendors")]
        [Description("Vendors Permissions")]
        public static class Vendors
        {
            public const string View = "retail:vendors:view";
            public const string Search = "retail:vendors:search";
            public const string Create = "retail:vendors:create";
            public const string Update = "retail:vendors:update";
            public const string Delete = "retail:vendors:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Vendors));
        }

        [DisplayName("WeekendCampaignProductDiscount")]
        [Description("WeekendCampaignProductDiscount Permissions")]
        public static class WeekendCampaignProductDiscount
        {
            public const string View = "retail:weekend-campaign-product-discount:view";
            public const string Search = "retail:weekend-campaign-product-discount:search";
            public const string Create = "retail:weekend-campaign-product-discount:create";
            public const string Update = "retail:weekend-campaign-product-discount:update";
            public const string Delete = "retail:weekend-campaign-product-discount:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(WeekendCampaignProductDiscount));
        }

        [DisplayName("Dashboards")]
        [Description("Dashboards Permissions")]
        public static class Dashboards
        {
            public const string View = "retail:dashboards:view";
            public const string Search = "retail:dashboards:search";
            public const string Create = "retail:dashboards:create";
            public const string Update = "retail:dashboards:update";
            public const string Delete = "retail:dashboards:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Dashboards));
        }

        [DisplayName("Categories")]
        [Description("Categories Permissions")]
        public static class Categories
        {
            public const string View = "retail:categories:view";
            public const string Search = "retail:categories:search";
            public const string Create = "retail:categories:create";
            public const string Update = "retail:categories:update";
            public const string Delete = "retail:categories:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Categories));
        }

        [DisplayName("ProductExportConfiguration")]
        [Description("ProductExportConfiguration Permissions")]
        public static class ProductExportConfiguration
        {
            public const string View = "retail:product-export-configuration:view";
            public const string Search = "retail:product-export-configuration:search";
            public const string Create = "retail:product-export-configuration:create";
            public const string Update = "retail:product-export-configuration:update";
            public const string Delete = "retail:product-export-configuration:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductExportConfiguration));
        }

        [DisplayName("VendorSupplierConfiguration")]
        [Description("VendorSupplierConfiguration Permissions")]
        public static class VendorSupplierConfiguration
        {
            public const string View = "retail:vendor-supplier-configuration:view";
            public const string Search = "retail:vendor-supplier-configuration:search";
            public const string Create = "retail:vendor-supplier-configuration:create";
            public const string Update = "retail:vendor-supplier-configuration:update";
            public const string Delete = "retail:vendor-supplier-configuration:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(VendorSupplierConfiguration));
        }

        [DisplayName("Product")]
        [Description("Product Permissions")]
        public static class Product
        {
            public const string View = "retail:product:view";
            public const string Search = "retail:product:search";
            public const string Create = "retail:product:create";
            public const string Update = "retail:product:update";
            public const string Delete = "retail:product:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Product));
        }

        [DisplayName("ProductProposal")]
        [Description("ProductProposal Permissions")]
        public static class ProductProposal
        {
            public const string View = "retail:product-proposal:view";
            public const string Search = "retail:product-proposal:search";
            public const string Create = "retail:product-proposal:create";
            public const string Update = "retail:product-proposal:update";
            public const string Delete = "retail:product-proposal:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(ProductProposal));
        }

        [DisplayName("WeekendCampaign")]
        [Description("WeekendCampaign Permissions")]
        public static class WeekendCampaign
        {
            public const string View = "retail:weekend-campaign:view";
            public const string Search = "retail:weekend-campaign:search";
            public const string Create = "retail:weekend-campaign:create";
            public const string Update = "retail:weekend-campaign:update";
            public const string Delete = "retail:weekend-campaign:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(WeekendCampaign));
        }

        [DisplayName("LeafletRegistration")]
        [Description("LeafletRegistration Permissions")]
        public static class LeafletRegistration
        {
            public const string View = "retail:leaflet-registration:view";
            public const string Search = "retail:leaflet-registration:search";
            public const string Create = "retail:leaflet-registration:create";
            public const string Update = "retail:leaflet-registration:update";
            public const string Delete = "retail:leaflet-registration:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(LeafletRegistration));
        }

        [DisplayName("Discounts")]
        [Description("Discounts Permissions")]
        public static class Discounts
        {
            public const string View = "retail:discounts:view";
            public const string Search = "retail:discounts:search";
            public const string Create = "retail:discounts:create";
            public const string Update = "retail:discounts:update";
            public const string Delete = "retail:discounts:delete";

            public static HashSet<string> PermissionList => GeneratePermissionsForModule(nameof(Discounts));
        }

        private static readonly ConcurrentDictionary<string, HashSet<string>> _modulePermissionsCache = new();

        public static HashSet<string> GeneratePermissionsForModule(string module) =>
            _modulePermissionsCache.GetOrAdd(module.ToLowerInvariant(), key =>
            [
                $"retail:{key}:create",
                $"retail:{key}:view",
                $"retail:{key}:update",
                $"retail:{key}:delete",
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
