namespace Jezda.Common.Contracts.Permissions;

/// <summary>
/// Request sent from microservices to Nexus during startup to register their permissions.
/// </summary>
public class PermissionRegistrationRequest
{
    /// <summary>
    /// Unique service code matching ApplicationModule.Code (e.g., "tms", "hrms", "retail")
    /// </summary>
    public string ServiceCode { get; set; } = default!;

    /// <summary>
    /// Human-readable service name (e.g., "Task Management System")
    /// </summary>
    public string ServiceName { get; set; } = default!;

    /// <summary>
    /// Service version (semantic versioning, e.g., "1.2.3")
    /// </summary>
    public string Version { get; set; } = default!;

    /// <summary>
    /// List of permissions to register
    /// </summary>
    public List<PermissionRegistrationDto> Permissions { get; set; } = [];
}

