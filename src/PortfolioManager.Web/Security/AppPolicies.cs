using Microsoft.AspNetCore.Authorization;
using PortfolioManager.Data.Identity;

namespace PortfolioManager.Web.Security;

/// <summary>
/// App level: every endpoint and page requires a signed-in user (fallback policy); only the login page opts out.
/// Role level: 'AdminOnly' for the administration pages.
/// </summary>
public static class AppPolicies
{
    public const string Authenticated = "Authenticated";
    public const string AdminOnly = "AdminOnly";

    public static void Configure(AuthorizationOptions options)
    {
        var authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        options.AddPolicy(Authenticated, authenticated);
        options.AddPolicy(AdminOnly, policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        options.FallbackPolicy = authenticated;
    }
}
