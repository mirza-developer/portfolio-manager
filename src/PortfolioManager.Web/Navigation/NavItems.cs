namespace PortfolioManager.Web.Navigation;

public sealed record NavItem(string Title, string Href, string Group, bool AdminOnly);

/// <summary>The sidebar, as data. Every page gets exactly one entry; admin pages are visible to admins only.</summary>
public static class NavItems
{
    public const string TradingGroup = "Trading";
    public const string AdministrationGroup = "Administration";

    public static IReadOnlyList<NavItem> All { get; } = new List<NavItem>
    {
        new("Dashboard", "/", TradingGroup, false),
        new("Portfolios", "/portfolios", TradingGroup, false),
        new("Trade logs", "/logs/trades", TradingGroup, false),
        new("Price logs", "/logs/prices", TradingGroup, false),
        new("Assets", "/admin/assets", AdministrationGroup, true),
        new("Users", "/admin/users", AdministrationGroup, true)
    };

    public static IReadOnlyList<NavItem> VisibleFor(bool isAdmin) => All.Where(i => isAdmin || !i.AdminOnly).ToList();
}
