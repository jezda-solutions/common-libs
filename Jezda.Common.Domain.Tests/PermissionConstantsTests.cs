using Jezda.Common.Domain.Constants;
using Xunit;

namespace Jezda.Common.Domain.Tests;

public class PermissionConstantsTests
{
    [Theory]
    [InlineData(typeof(PermissionConstants.Nexus.Users), "nexus", "users")]
    [InlineData(typeof(PermissionConstants.Nexus.Organisations), "nexus", "organisations")]
    [InlineData(typeof(PermissionConstants.Nexus.Roles), "nexus", "roles")]
    [InlineData(typeof(PermissionConstants.Nexus.Permissions), "nexus", "permissions")]
    [InlineData(typeof(PermissionConstants.Nexus.UserClaims), "nexus", "user-claims")]
    [InlineData(typeof(PermissionConstants.Nexus.Currencies), "nexus", "currencies")]
    [InlineData(typeof(PermissionConstants.HRMS.Employee), "hrms", "employee")]
    [InlineData(typeof(PermissionConstants.HRMS.Department), "hrms", "department")]
    [InlineData(typeof(PermissionConstants.HRMS.Position), "hrms", "position")]
    [InlineData(typeof(PermissionConstants.HRMS.Location), "hrms", "location")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItem), "tms", "work-item")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItemAssignment), "tms", "work-item-assignment")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItemComment), "tms", "work-item-comment")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItemAttachment), "tms", "work-item-attachment")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItemDependency), "tms", "work-item-dependency")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItemStatus), "tms", "work-item-status")]
    [InlineData(typeof(PermissionConstants.TMS.WorkItemTag), "tms", "work-item-tag")]
    [InlineData(typeof(PermissionConstants.TMS.Project), "tms", "project")]
    [InlineData(typeof(PermissionConstants.TMS.TimeLog), "tms", "time-log")]
    [InlineData(typeof(PermissionConstants.TMS.OrganisationGlossary), "tms", "organisation-glossary")]
    [InlineData(typeof(PermissionConstants.Retail.Addresses), "retail", "addresses")]
    [InlineData(typeof(PermissionConstants.Retail.Brands), "retail", "brands")]
    [InlineData(typeof(PermissionConstants.Retail.Categories), "retail", "categories")]
    [InlineData(typeof(PermissionConstants.Retail.Products), "retail", "products")]
    public void StandardPermissions_ShouldHaveAllRequiredFields(Type permissionClass, string module, string resource)
    {
        // Act & Assert - This will throw if validation fails
        PermissionConstants.PermissionValidator.ValidateStandardPermissions(permissionClass, module, resource);
    }

    [Fact]
    public void AllPermissionClasses_ShouldBeDiscoverable()
    {
        // Arrange & Act
        var allClasses = PermissionConstants.PermissionValidator.GetAllPermissionClasses().ToList();

        // Assert
        Assert.NotEmpty(allClasses);
        Assert.Contains(allClasses, t => t.Name == "Users");
        Assert.Contains(allClasses, t => t.Name == "WorkItem");
        Assert.Contains(allClasses, t => t.Name == "Employee");
    }

    [Fact]
    public void AllRetailPermissionClasses_MustHaveStandardCRUD()
    {
        // Arrange
        var retailType = typeof(PermissionConstants.Retail);
        var allNestedClasses = retailType.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        // Classes with only custom operations (not standard CRUD)
        var excludedClasses = new HashSet<string> { };

        // Act & Assert
        foreach (var permissionClass in allNestedClasses)
        {
            if (excludedClasses.Contains(permissionClass.Name))
                continue;

            var resourceName = ToKebabCase(permissionClass.Name);
            PermissionConstants.PermissionValidator.ValidateStandardPermissions(permissionClass, "retail", resourceName);
        }
    }

    [Fact]
    public void AllTMSPermissionClasses_MustHaveStandardCRUD()
    {
        // Arrange
        var tmsType = typeof(PermissionConstants.TMS);
        var allNestedClasses = tmsType.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        // Classes with only custom operations (not standard CRUD)
        var excludedClasses = new HashSet<string>
        {
            "WorkItemBulk",  // Has only Start, Cancel, View, Search, Update, Delete (no Create - bulk operations don't create)
            "WorkItemDependencyAnalysis",  // Has only ViewGraph, ViewImpact (read-only analysis)
            "WorkItemStatusTemplate",  // Has only View, Apply (templates are read-only)
            "WorkItemTypeTemplate",  // Has only View, Apply (templates are read-only)
            "ProjectMember",  // Has only Add, Remove, View (not full CRUD)
            "OrganisationSettings",  // Has only View, Update (system settings, no Create/Delete)
            "OnboardingStatus",  // Has only View (read-only status)
            "TaskSetTemplate",  // Has only View, Apply (templates are read-only)
            "Mentions"  // Has only View (read-only mentions/notifications)
        };

        // Act & Assert
        foreach (var permissionClass in allNestedClasses)
        {
            if (excludedClasses.Contains(permissionClass.Name))
                continue;

            var resourceName = ToKebabCase(permissionClass.Name);
            PermissionConstants.PermissionValidator.ValidateStandardPermissions(permissionClass, "tms", resourceName);
        }
    }

    [Fact]
    public void AllHRMSPermissionClasses_MustHaveStandardCRUD()
    {
        // Arrange
        var hrmsType = typeof(PermissionConstants.HRMS);
        var allNestedClasses = hrmsType.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        // Classes with only custom operations (not standard CRUD)
        var excludedClasses = new HashSet<string>
        {
            "Profile",  // Has only View (read-only profile)
            "Dashboard"  // Has only View (read-only dashboard)
        };

        // Custom resource name mappings (when class name doesn't match permission resource name)
        var customResourceNames = new Dictionary<string, string>
        {
            { "EmergencyContact", "employee-emergency-contact" }
        };

        // Act & Assert
        foreach (var permissionClass in allNestedClasses)
        {
            if (excludedClasses.Contains(permissionClass.Name))
                continue;

            var resourceName = customResourceNames.ContainsKey(permissionClass.Name)
                ? customResourceNames[permissionClass.Name]
                : ToKebabCase(permissionClass.Name);

            PermissionConstants.PermissionValidator.ValidateStandardPermissions(permissionClass, "hrms", resourceName);
        }
    }

    [Fact]
    public void AllNexusPermissionClasses_MustHaveStandardCRUD()
    {
        // Arrange
        var nexusType = typeof(PermissionConstants.Nexus);
        var allNestedClasses = nexusType.GetNestedTypes(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        // Classes with only custom operations (not standard CRUD)
        var excludedClasses = new HashSet<string>
        {
            "Admin"  // Has only ViewAll, ManageSystem, AccessAdminPanel (system-level permissions)
        };

        // Act & Assert
        foreach (var permissionClass in allNestedClasses)
        {
            if (excludedClasses.Contains(permissionClass.Name))
                continue;

            var resourceName = ToKebabCase(permissionClass.Name);
            PermissionConstants.PermissionValidator.ValidateStandardPermissions(permissionClass, "nexus", resourceName);
        }
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        return System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
    }

    [Fact]
    public void StandardPermissions_ShouldGenerateCorrectValues()
    {
        // Arrange
        var perms = new PermissionConstants.StandardPermissions("test", "resource");

        // Assert
        Assert.Equal("test:resource:view", perms.View);
        Assert.Equal("test:resource:search", perms.Search);
        Assert.Equal("test:resource:create", perms.Create);
        Assert.Equal("test:resource:update", perms.Update);
        Assert.Equal("test:resource:delete", perms.Delete);
    }

    [Fact]
    public void StandardPermissions_ShouldReturnAllPermissions()
    {
        // Arrange
        var perms = new PermissionConstants.StandardPermissions("test", "resource");

        // Act
        var all = perms.GetAllPermissions();

        // Assert
        Assert.Equal(5, all.Count);
        Assert.Contains("test:resource:view", all);
        Assert.Contains("test:resource:search", all);
        Assert.Contains("test:resource:create", all);
        Assert.Contains("test:resource:update", all);
        Assert.Contains("test:resource:delete", all);
    }
}
