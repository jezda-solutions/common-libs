namespace Jezda.Common.Contracts.Permissions;

/// <summary>
/// Represents a single permission to be registered.
/// </summary>
public class PermissionRegistrationDto
{
    /// <summary>
    /// Permission code following pattern: module:resource:action
    /// Example: "tms:work-item:create"
    /// </summary>
    public string Code { get; set; } = default!;

    /// <summary>
    /// Human-readable description of what this permission allows
    /// Example: "Create work items"
    /// </summary>
    public string Description { get; set; } = default!;
}

