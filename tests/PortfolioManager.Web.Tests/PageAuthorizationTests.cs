using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using PortfolioManager.Web.Navigation;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Tests;

/// <summary>Ties the pages, the sidebar and the authorization rules together so none of them can drift apart.</summary>
public class PageAuthorizationTests
{
    private static readonly Assembly WebAssembly = typeof(NavItems).Assembly;

    private static IReadOnlyList<(Type Page, string Route)> Pages() =>
        WebAssembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes<RouteAttribute>().Select(r => (Page: t, Route: r.Template)))
            .ToList();

    [Fact]
    public void Every_sidebar_link_points_to_a_real_page()
    {
        var routes = Pages().Select(p => p.Route).ToList();
        foreach (var item in NavItems.All)
            Assert.Contains(routes, r => r == item.Href);
    }

    [Fact]
    public void Every_page_that_needs_a_sidebar_link_has_one()
    {
        // pages with a route parameter (details pages) and the login page are reached from other pages, not from the sidebar
        var linked = NavItems.All.Select(i => i.Href).ToHashSet();
        foreach (var (page, route) in Pages().Where(p => !p.Route.Contains('{') && !p.Route.StartsWith("/account")))
            Assert.True(linked.Contains(route), $"{page.Name} ({route}) has no sidebar link.");
    }

    [Fact]
    public void Administration_pages_require_the_AdminOnly_policy()
    {
        foreach (var item in NavItems.All.Where(i => i.AdminOnly))
        {
            var page = Pages().Single(p => p.Route == item.Href).Page;
            var authorize = page.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();
            Assert.NotNull(authorize);
            Assert.Equal(AppPolicies.AdminOnly, authorize!.Policy);
        }
    }

    [Fact]
    public void Other_pages_do_not_loosen_the_app_level_rule()
    {
        // Only the login page may be anonymous; everything else falls back to 'signed in' (see AppPolicies).
        foreach (var (page, route) in Pages())
        {
            var anonymous = page.GetCustomAttributes<AllowAnonymousAttribute>().Any();
            Assert.Equal(route == "/account/login", anonymous);
        }
    }

    [Fact]
    public void Every_page_is_registered_for_a_unique_route()
    {
        var routes = Pages().Select(p => p.Route).ToList();
        Assert.Equal(routes.Count, routes.Distinct().Count());
    }
}
