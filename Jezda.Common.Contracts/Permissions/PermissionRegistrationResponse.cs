namespace Jezda.Common.Contracts.Permissions;

/// <summary>
/// Response from Nexus after processing permission registration.
/// </summary>
public class PermissionRegistrationResponse
{
    /// <summary>
    /// Whether registration was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message describing the result
    /// </summary>
    public string Message { get; set; } = default!;

    /// <summary>
    /// Number of new permissions that were added
    /// </summary>
    public int NewPermissionsCount { get; set; }

    /// <summary>
    /// Number of permissions that already existed
    /// </summary>
    public int ExistingPermissionsCount { get; set; }

    /// <summary>
    /// List of permission codes that were added (for logging/auditing)
    /// </summary>
    public List<string> AddedPermissions { get; set; } = [];
}

