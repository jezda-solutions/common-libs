using Jezda.Common.Abstractions.Identity;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Jezda.Common.Helpers.Identity;

public abstract class BaseUserContext(IHttpContextAccessor accessor) : IUserContext
{
    private readonly IHttpContextAccessor _accessor = accessor;

    protected ClaimsPrincipal User => _accessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : Guid.Empty;

    public string Email => User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public string UserName => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public Guid? CurrentOrganisationId => Guid.TryParse(User.FindFirstValue("current_org_id"), out var orgId)
        ? orgId
        : null;

    public IReadOnlyList<string> Roles => [.. User.FindAll(ClaimTypes.Role).Select(x => x.Value).Distinct()];

    public IReadOnlyList<string> Permissions => [.. User.FindAll("permissions").Select(x => x.Value).Distinct()];

    public bool IsAuthenticated => User.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin => Roles.Contains("nexus_super_admin") || Roles.Contains("nexus_admin");

    public bool IsSupport => Roles.Contains("nexus_support");

    public string? GetClaim(string type) => User.FindFirstValue(type);

    public IEnumerable<string> GetClaims(string type) =>
        User.FindAll(type).Select(c => c.Value);
}
