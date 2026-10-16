using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data.Identity;
using PortfolioManager.Web.Services;

namespace PortfolioManager.Web.Tests;

public class AssetAdminServiceTests
{
    private static IAssetAdminService Service(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IAssetAdminService>();

    [Fact]
    public async Task Anyone_can_list_assets_but_only_an_admin_can_change_them()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        using var scope = db.NewScope();

        Assert.Equal(2, (await Service(scope).ListAsync()).Count); // GOLD and SILVER from the seeder
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).SaveAsync(alice, new AssetEdit { Symbol = "DOGE", Name = "Doge" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).SetEnabledAsync(alice, 1, false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).DeleteAsync(alice, 1));
    }

    [Fact]
    public async Task A_new_asset_is_added_as_data_with_a_normalized_symbol()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();

        var id = await Service(scope).SaveAsync(root, new AssetEdit { Symbol = "  doge ", Name = " Doge ", MinTradeSizeUsdt = 5m, PriceSource = "simulated" });

        var saved = (await Service(scope).ListAsync()).Single(a => a.Id == id);
        Assert.Equal("DOGE", saved.Symbol);
        Assert.Equal("Doge", saved.Name);
        Assert.True(saved.IsEnabled);
        Assert.Equal(5m, saved.MinTradeSizeUsdt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("HAS SPACE")]
    [InlineData("BAD-SYM")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public async Task Invalid_symbols_are_rejected(string symbol)
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).SaveAsync(root, new AssetEdit { Symbol = symbol, Name = "x" }));
    }

    [Fact]
    public async Task Other_invalid_fields_are_rejected()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).SaveAsync(root, new AssetEdit { Symbol = "DOGE", Name = " " }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).SaveAsync(root, new AssetEdit { Symbol = "DOGE", Name = "Doge", PriceSource = "" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).SaveAsync(root, new AssetEdit { Symbol = "DOGE", Name = "Doge", MinTradeSizeUsdt = -1m }));
    }

    [Fact]
    public async Task A_duplicate_symbol_is_rejected_but_an_asset_can_keep_its_own_symbol_when_edited()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).SaveAsync(root, new AssetEdit { Symbol = "gold", Name = "Another gold" }));
        await Service(scope).SaveAsync(root, new AssetEdit { Id = 1, Symbol = "GOLD", Name = "Renamed gold", MinTradeSizeUsdt = 1m });
        Assert.Equal("Renamed gold", (await Service(scope).ListAsync()).Single(a => a.Id == 1).Name);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(scope).SaveAsync(root, new AssetEdit { Id = 999, Symbol = "NOPE", Name = "x" }));
    }

    [Fact]
    public async Task An_asset_can_be_disabled_and_enabled()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();

        await Service(scope).SetEnabledAsync(root, 1, false);
        Assert.False((await Service(scope).ListAsync()).Single(a => a.Id == 1).IsEnabled);
        await Service(scope).SetEnabledAsync(root, 1, true);
        Assert.True((await Service(scope).ListAsync()).Single(a => a.Id == 1).IsEnabled);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(scope).SetEnabledAsync(root, 999, true));
    }

    [Fact]
    public async Task An_unused_asset_can_be_deleted_but_a_used_one_must_be_disabled_instead()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        await db.AddPortfolioAsync(root.UserId, "p", 0m, (1, 1m, 10m)); // GOLD is now in use
        using var scope = db.NewScope();

        var doge = await Service(scope).SaveAsync(root, new AssetEdit { Symbol = "DOGE", Name = "Doge" });
        await Service(scope).DeleteAsync(root, doge);
        Assert.DoesNotContain(await Service(scope).ListAsync(), a => a.Symbol == "DOGE");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).DeleteAsync(root, 1));
        Assert.Contains("Disable it instead", ex.Message);
        Assert.Contains(await Service(scope).ListAsync(), a => a.Symbol == "GOLD");
    }
}

public class UserAdminServiceTests
{
    private static IUserAdminService Service(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IUserAdminService>();

    [Fact]
    public async Task Only_an_admin_can_use_the_user_administration()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        using var scope = db.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).ListAsync(alice));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).CreateAsync(alice, new UserEdit { Email = "x@example.com", Password = WebTestDatabase.Password }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).DeleteAsync(alice, "anything"));
    }

    [Fact]
    public async Task An_admin_creates_a_user_with_a_role_and_the_list_shows_it()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();

        var id = await Service(scope).CreateAsync(root, new UserEdit { Email = "new@example.com", DisplayName = "New One", Role = AppRoles.User, Password = WebTestDatabase.Password });

        var row = (await Service(scope).ListAsync(root)).Single(u => u.Id == id);
        Assert.Equal("new@example.com", row.Email);
        Assert.Equal("New One", row.DisplayName);
        Assert.Equal(AppRoles.User, row.Role);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task Invalid_new_users_are_rejected()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreateAsync(root, new UserEdit { Email = "not-an-email", Password = WebTestDatabase.Password }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreateAsync(root, new UserEdit { Email = "a@example.com", Role = "Boss", Password = WebTestDatabase.Password }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreateAsync(root, new UserEdit { Email = "a@example.com" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreateAsync(root, new UserEdit { Email = "a@example.com", Password = "123" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreateAsync(root, new UserEdit { Email = "root@example.com", Password = WebTestDatabase.Password })); // duplicate
    }

    [Fact]
    public async Task Updating_a_user_changes_name_role_and_active_flag()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        var alice = await db.AddUserAsync("alice@example.com", false);
        using var scope = db.NewScope();

        await Service(scope).UpdateAsync(root, new UserEdit { Id = alice.UserId, DisplayName = "Alice A.", Role = AppRoles.Admin, IsActive = false });

        var row = (await Service(scope).ListAsync(root)).Single(u => u.Id == alice.UserId);
        Assert.Equal("Alice A.", row.DisplayName);
        Assert.Equal(AppRoles.Admin, row.Role);
        Assert.False(row.IsActive);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(scope).UpdateAsync(root, new UserEdit { Id = "missing", Role = AppRoles.User }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).UpdateAsync(root, new UserEdit { Id = alice.UserId, Role = "Boss" }));
    }

    [Fact]
    public async Task An_admin_cannot_lock_demote_or_delete_themselves()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        using var scope = db.NewScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).UpdateAsync(root, new UserEdit { Id = root.UserId, Role = AppRoles.Admin, IsActive = false }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).UpdateAsync(root, new UserEdit { Id = root.UserId, Role = AppRoles.User, IsActive = true }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).DeleteAsync(root, root.UserId));
    }

    [Fact]
    public async Task Resetting_the_password_replaces_it()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        var alice = await db.AddUserAsync("alice@example.com", false);
        using var scope = db.NewScope();

        await Service(scope).ResetPasswordAsync(root, alice.UserId, "Brand#New9");

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(alice.UserId))!;
        Assert.True(await users.CheckPasswordAsync(user, "Brand#New9"));
        Assert.False(await users.CheckPasswordAsync(user, WebTestDatabase.Password));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).ResetPasswordAsync(root, alice.UserId, "123"));
    }

    [Fact]
    public async Task A_user_without_portfolios_can_be_deleted_but_one_who_owns_portfolios_cannot()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        var alice = await db.AddUserAsync("alice@example.com", false);
        var bob = await db.AddUserAsync("bob@example.com", false);
        await db.AddPortfolioAsync(bob.UserId, "p", 0m);
        using var scope = db.NewScope();

        await Service(scope).DeleteAsync(root, alice.UserId);
        Assert.DoesNotContain(await Service(scope).ListAsync(root), u => u.Id == alice.UserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).DeleteAsync(root, bob.UserId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(scope).DeleteAsync(root, "missing"));
    }
}

public class DashboardServiceTests
{
    [Fact]
    public async Task The_dashboard_totals_cover_only_what_the_user_may_see()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var bob = await db.AddUserAsync("bob@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var a = await db.AddPortfolioAsync(alice.UserId, "a", 100m, (1, 5m, 500m));
        var b = await db.AddPortfolioAsync(bob.UserId, "b", 40m);
        await db.AddPriceAsync(1, 100m, DateTime.UtcNow);
        var tradeTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        await db.AddTradeAsync(a, 1, TradeSide.Buy, tradeTime);
        await db.AddTradeAsync(b, 1, TradeSide.Buy, tradeTime.AddHours(1));

        using var scope = db.NewScope();
        var service = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        var mine = await service.GetAsync(alice);
        Assert.Equal(600m, mine.TotalValueUsdt);     // 100 USDT + 5 x 100
        Assert.Equal(100m, mine.UsdtBalance);
        Assert.Equal(1, mine.HoldingCount);
        Assert.Equal(tradeTime, mine.LastTradeUtc);
        Assert.Single(mine.LatestTrades);

        var all = await service.GetAsync(root);
        Assert.Equal(640m, all.TotalValueUsdt);
        Assert.Equal(140m, all.UsdtBalance);
        Assert.Equal(2, all.LatestTrades.Count);
        Assert.Equal(tradeTime.AddHours(1), all.LastTradeUtc);
    }

    [Fact]
    public async Task The_dashboard_is_empty_but_valid_without_data()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        using var scope = db.NewScope();
        var data = await scope.ServiceProvider.GetRequiredService<IDashboardService>().GetAsync(alice);
        Assert.Equal(0m, data.TotalValueUsdt);
        Assert.Null(data.LastTradeUtc);
        Assert.Empty(data.LatestTrades);
    }
}
