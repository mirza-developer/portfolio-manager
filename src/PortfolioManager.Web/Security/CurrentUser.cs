using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PortfolioManager.Data.Identity;

namespace PortfolioManager.Web.Security;

/// <summary>Who is calling a service. Services enforce ownership and admin rules with this, never the UI alone.</summary>
public sealed record UserContext(string UserId, bool IsAdmin, string DisplayName = "")
{
    public static UserContext FromPrincipal(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? throw new UnauthorizedAccessException("The user is not signed in.");
        return new UserContext(id, principal.IsInRole(AppRoles.Admin), principal.Identity?.Name ?? string.Empty);
    }
}

public interface ICurrentUser
{
    Task<UserContext> GetAsync();
}

public sealed class CurrentUser(AuthenticationStateProvider provider) : ICurrentUser
{
    public async Task<UserContext> GetAsync() => UserContext.FromPrincipal((await provider.GetAuthenticationStateAsync()).User);
}
