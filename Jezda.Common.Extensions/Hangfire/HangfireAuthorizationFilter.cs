using Hangfire.Dashboard;
using Jezda.Common.Abstractions.Configuration.Options;
using System;
using System.Linq;
using System.Text;

namespace Jezda.Common.Extensions.Hangfire;

public class HangfireAuthorizationFilter(
    HangfireOptions options) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();

        if (authHeader != null && authHeader.StartsWith("Basic "))
        {
            var encodedUsernamePassword = authHeader["Basic ".Length..].Trim();
            var usernamePassword = Encoding.UTF8.GetString(Convert.FromBase64String(encodedUsernamePassword));
            var parts = usernamePassword.Split(':', 2);

            if (parts.Length == 2)
            {
                var username = parts[0];
                var password = parts[1];

                return username == options.Authorization.Username && password == options.Authorization.Password;
            }
        }

        httpContext.Response.Headers.WWWAuthenticate = "Basic realm=\"Hangfire Dashboard\"";
        httpContext.Response.StatusCode = 401;

        return false;
    }
}