using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioManager.Data.Identity;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Tests;

public class AppPoliciesTests
{
    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "u1"), new(ClaimTypes.Name, "u1@example.com") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(AppPolicies.Configure);
        return services.BuildServiceProvider();
    }

    private static async Task<bool> Allowed(ClaimsPrincipal user, string policy)
    {
        using var provider = Services();
        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, policy);
        return result.Succeeded;
    }

    [Fact]
    public async Task Anonymous_users_are_denied_by_both_policies()
    {
        Assert.False(await Allowed(Anonymous(), AppPolicies.Authenticated));
        Assert.False(await Allowed(Anonymous(), AppPolicies.AdminOnly));
    }

    [Fact]
    public async Task A_user_with_the_User_role_passes_Authenticated_but_not_AdminOnly()
    {
        Assert.True(await Allowed(SignedIn(AppRoles.User), AppPolicies.Authenticated));
        Assert.False(await Allowed(SignedIn(AppRoles.User), AppPolicies.AdminOnly));
    }

    [Fact]
    public async Task A_signed_in_user_without_any_role_is_not_an_admin()
    {
        Assert.True(await Allowed(SignedIn(), AppPolicies.Authenticated));
        Assert.False(await Allowed(SignedIn(), AppPolicies.AdminOnly));
    }

    [Fact]
    public async Task An_admin_passes_both_policies()
    {
        Assert.True(await Allowed(SignedIn(AppRoles.Admin), AppPolicies.Authenticated));
        Assert.True(await Allowed(SignedIn(AppRoles.Admin), AppPolicies.AdminOnly));
    }

    [Fact]
    public async Task The_fallback_policy_requires_a_signed_in_user_everywhere()
    {
        using var provider = Services();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = await policies.GetFallbackPolicyAsync();
        Assert.NotNull(fallback);

        var authorization = provider.GetRequiredService<IAuthorizationService>();
        Assert.False((await authorization.AuthorizeAsync(Anonymous(), null, fallback!)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(SignedIn(AppRoles.User), null, fallback!)).Succeeded);
    }

    [Fact]
    public void UserContext_is_built_from_the_claims()
    {
        var admin = UserContext.FromPrincipal(SignedIn(AppRoles.Admin));
        Assert.Equal("u1", admin.UserId);
        Assert.True(admin.IsAdmin);
        Assert.Equal("u1@example.com", admin.DisplayName);
        Assert.False(UserContext.FromPrincipal(SignedIn(AppRoles.User)).IsAdmin);
    }

    [Fact]
    public void UserContext_rejects_an_anonymous_principal()
    {
        Assert.Throws<UnauthorizedAccessException>(() => { UserContext.FromPrincipal(Anonymous()); });
    }
}
