using PortfolioManager.Web.Navigation;

namespace PortfolioManager.Web.Tests;

public class NavItemsTests
{
    [Fact]
    public void A_regular_user_does_not_see_the_administration_links()
    {
        var visible = NavItems.VisibleFor(isAdmin: false);
        Assert.True(visible.All(i => !i.AdminOnly));
        Assert.True(visible.All(i => i.Group == NavItems.TradingGroup));
        Assert.DoesNotContain(visible, i => i.Href == "/admin/users");
        Assert.DoesNotContain(visible, i => i.Href == "/admin/assets");
    }

    [Fact]
    public void An_admin_sees_every_link()
    {
        var visible = NavItems.VisibleFor(isAdmin: true);
        Assert.Equal(NavItems.All.Count, visible.Count);
        Assert.Contains(visible, i => i.Href == "/admin/users");
        Assert.Contains(visible, i => i.Href == "/admin/assets");
    }

    [Fact]
    public void Every_administration_link_is_marked_admin_only_and_the_rest_are_not()
    {
        foreach (var item in NavItems.All)
            Assert.Equal(item.Group == NavItems.AdministrationGroup, item.AdminOnly);
    }

    [Fact]
    public void Links_are_unique_absolute_paths()
    {
        Assert.Equal(NavItems.All.Count, NavItems.All.Select(i => i.Href).Distinct().Count());
        Assert.True(NavItems.All.All(i => i.Href.StartsWith('/')));
    }
}
