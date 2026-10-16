using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Core.Domain;
using PortfolioManager.Web.Services;

namespace PortfolioManager.Web.Tests;

public class PortfolioServiceTests
{
    private static IPortfolioService Service(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<IPortfolioService>();

    [Fact]
    public async Task A_user_sees_only_their_own_portfolios_and_an_admin_sees_all()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", admin: false);
        var bob = await db.AddUserAsync("bob@example.com", admin: false);
        var root = await db.AddUserAsync("root@example.com", admin: true);
        await db.AddPortfolioAsync(alice.UserId, "alice-1", 100m);
        await db.AddPortfolioAsync(bob.UserId, "bob-1", 200m);

        using var scope = db.NewScope();
        var mine = await Service(scope).ListAsync(alice);
        var all = await Service(scope).ListAsync(root);

        Assert.Equal("alice-1", Assert.Single(mine).Name);
        Assert.Equal(2, all.Count);
        Assert.Equal("alice@example.com", all.Single(p => p.Name == "alice-1").OwnerName);
    }

    [Fact]
    public async Task Another_users_portfolio_is_not_visible_but_an_admin_can_open_it()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var bob = await db.AddUserAsync("bob@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var id = await db.AddPortfolioAsync(alice.UserId, "alice-1", 100m);

        using var scope = db.NewScope();
        Assert.Null(await Service(scope).GetAsync(bob, id));
        Assert.NotNull(await Service(scope).GetAsync(alice, id));
        Assert.NotNull(await Service(scope).GetAsync(root, id));
        Assert.Null(await Service(scope).GetAsync(root, 99999));
    }

    [Fact]
    public async Task The_value_uses_the_latest_logged_price_of_each_asset()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 50m, (1, 10m, 1000m));
        await db.AddPriceAsync(1, 80m, DateTime.UtcNow.AddHours(-2));
        await db.AddPriceAsync(1, 120m, DateTime.UtcNow.AddHours(-1)); // the latest

        using var scope = db.NewScope();
        var details = await Service(scope).GetAsync(alice, id);

        Assert.Equal(1250m, details!.TotalValueUsdt);      // 50 USDT + 10 x 120
        var position = Assert.Single(details.Positions);
        Assert.Equal("GOLD", position.Symbol);
        Assert.Equal(1200m, position.ValueUsdt);
        Assert.True(position.HasPrice);
        Assert.Equal(0m, position.DrawdownPercent);        // 1200 is above the 1000 benchmark
    }

    [Fact]
    public async Task A_position_without_a_price_is_flagged_and_excluded_from_the_value()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 50m, (1, 10m, 1000m));

        using var scope = db.NewScope();
        var details = await Service(scope).GetAsync(alice, id);

        Assert.Equal(50m, details!.TotalValueUsdt);
        Assert.False(details.Positions[0].HasPrice);
    }

    [Fact]
    public async Task The_owner_can_change_the_settings()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 0m);

        using var scope = db.NewScope();
        await Service(scope).UpdateSettingsAsync(alice, id, new PortfolioSettings
        {
            StopLossPercent = 7.5m, MaxAllocationPercent = 30m, CashReservePercent = 20m,
            CooldownHours = 2, ReentryRecoveryPercent = 3m, IsAutoTradingEnabled = false
        });

        using var context = db.NewContext();
        var saved = await context.Portfolios.SingleAsync(p => p.Id == id);
        Assert.Equal(7.5m, saved.StopLossPercent);
        Assert.Equal(30m, saved.MaxAllocationPercent);
        Assert.Equal(20m, saved.CashReservePercent);
        Assert.Equal(2, saved.CooldownHours);
        Assert.Equal(3m, saved.ReentryRecoveryPercent);
        Assert.False(saved.IsAutoTradingEnabled);
    }

    [Theory]
    [InlineData(0, 40, 10, 4, 1)]     // stop-loss must be > 0
    [InlineData(100, 40, 10, 4, 1)]   // stop-loss must be < 100
    [InlineData(5, 0, 10, 4, 1)]      // allocation must be > 0
    [InlineData(5, 101, 10, 4, 1)]    // allocation must be <= 100
    [InlineData(5, 40, -1, 4, 1)]     // reserve must be >= 0
    [InlineData(5, 40, 100, 4, 1)]    // reserve must be < 100
    [InlineData(5, 40, 10, -1, 1)]    // cooldown must be >= 0
    [InlineData(5, 40, 10, 721, 1)]   // cooldown must be <= 720
    [InlineData(5, 40, 10, 4, -1)]    // recovery must be >= 0
    [InlineData(5, 40, 10, 4, 101)]   // recovery must be <= 100
    public async Task Invalid_settings_are_rejected(int stop, int max, int reserve, int cooldown, int recovery)
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 0m);

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).UpdateSettingsAsync(alice, id, new PortfolioSettings
        {
            StopLossPercent = stop, MaxAllocationPercent = max, CashReservePercent = reserve, CooldownHours = cooldown, ReentryRecoveryPercent = recovery
        }));
    }

    [Fact]
    public async Task Another_user_cannot_change_the_settings()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var bob = await db.AddUserAsync("bob@example.com", false);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 0m);

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).UpdateSettingsAsync(bob, id, new PortfolioSettings()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(scope).UpdateSettingsAsync(alice, 99999, new PortfolioSettings()));
    }

    [Fact]
    public async Task Only_an_admin_can_create_portfolios_and_the_owner_must_exist()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).CreatePortfolioAsync(alice, alice.UserId, "mine", 10m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreatePortfolioAsync(root, "no-such-user", "x", 10m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).CreatePortfolioAsync(root, alice.UserId, "  ", 10m));

        var id = await Service(scope).CreatePortfolioAsync(root, alice.UserId, "created", 25m);
        Assert.NotNull(await Service(scope).GetAsync(alice, id));
    }

    [Fact]
    public async Task Only_an_admin_can_add_a_position_and_the_rules_are_enforced()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 0m);

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).AddPositionAsync(alice, id, 1, 1m, 100m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).AddPositionAsync(root, id, 1, 0m, 100m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).AddPositionAsync(root, id, 999, 1m, 100m));

        await Service(scope).AddPositionAsync(root, id, 1, 5m, 500m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).AddPositionAsync(root, id, 1, 5m, 500m)); // duplicate asset

        var details = await Service(scope).GetAsync(root, id);
        var position = Assert.Single(details!.Positions);
        Assert.Equal(PositionStatus.Holding, position.Status);
        Assert.Equal(500m, position.BenchmarkValueUsdt);
    }

    [Fact]
    public async Task A_disabled_asset_cannot_be_added_to_a_portfolio()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 0m);
        using (var context = db.NewContext())
        {
            (await context.Assets.SingleAsync(a => a.Symbol == "SILVER")).IsEnabled = false;
            await context.SaveChangesAsync();
        }

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).AddPositionAsync(root, id, 2, 1m, 10m));
    }

    [Fact]
    public async Task Editing_a_position_restarts_the_benchmark_at_v0_and_a_zero_quantity_clears_it()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 0m, (1, 10m, 1000m));
        int positionId;
        using (var context = db.NewContext()) positionId = (await context.Positions.SingleAsync()).Id;

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).UpdatePositionAsync(alice, id, positionId, new PositionEdit()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).UpdatePositionAsync(root, id, positionId, new PositionEdit { Quantity = 1m, InitialValueUsdt = 1m, StopLossPercentOverride = 100m }));

        await Service(scope).UpdatePositionAsync(root, id, positionId, new PositionEdit { Quantity = 4m, InitialValueUsdt = 800m, StopLossPercentOverride = 2m });
        using (var context = db.NewContext())
        {
            var p = await context.Positions.SingleAsync();
            Assert.Equal(4m, p.Quantity);
            Assert.Equal(800m, p.InitialValueUsdt);
            Assert.Equal(800m, p.BenchmarkValueUsdt);
            Assert.Equal(2m, p.StopLossPercentOverride);
            Assert.Equal(PositionStatus.Holding, p.Status);
        }

        await Service(scope).UpdatePositionAsync(root, id, positionId, new PositionEdit { Quantity = 0m, InitialValueUsdt = 800m });
        using (var context = db.NewContext())
        {
            var p = await context.Positions.SingleAsync();
            Assert.Equal(PositionStatus.None, p.Status);
            Assert.Equal(0m, p.BenchmarkValueUsdt);
        }
    }

    [Fact]
    public async Task Only_an_admin_can_change_the_usdt_balance()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var id = await db.AddPortfolioAsync(alice.UserId, "p", 10m);

        using var scope = db.NewScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(scope).UpdateUsdtBalanceAsync(alice, id, 999m));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(scope).UpdateUsdtBalanceAsync(root, id, -1m));
        await Service(scope).UpdateUsdtBalanceAsync(root, id, 321m);

        Assert.Equal(321m, (await Service(scope).GetAsync(root, id))!.UsdtBalance);
    }
}
