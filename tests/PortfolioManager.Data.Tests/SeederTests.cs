using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data.Identity;
using PortfolioManager.Data.Seeding;

namespace PortfolioManager.Data.Tests;

public class SeederTests
{
    private static void FullSeed(SeedOptions o)
    {
        o.AdminEmail = "admin@example.com";
        o.AdminPassword = "Admin#12345";
        o.CreateDemoPortfolio = true;
    }

    [Fact]
    public async Task Seeding_twice_creates_everything_exactly_once()
    {
        using var db = new TestDatabase(FullSeed);
        for (var i = 0; i < 2; i++)
        {
            using var scope = db.NewScope();
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
        }

        using var context = db.NewContext();
        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(1, await context.Users.CountAsync());
        Assert.Equal(2, await context.Assets.CountAsync());
        Assert.Equal(1, await context.Portfolios.CountAsync());
        Assert.Equal(2, await context.Positions.CountAsync());

        using var scope2 = db.NewScope();
        var users = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync("admin@example.com");
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin!, AppRoles.Admin));
        Assert.False(await users.IsInRoleAsync(admin!, AppRoles.User));
    }

    [Fact]
    public async Task The_demo_portfolio_has_usdt_and_two_holding_positions_with_benchmark_equal_to_v0()
    {
        using var db = new TestDatabase(FullSeed);
        using (var scope = db.NewScope())
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();

        using var context = db.NewContext();
        var portfolio = await context.Portfolios.Include(p => p.Positions).SingleAsync();
        Assert.Equal(1000m, portfolio.UsdtBalance);
        Assert.Equal(5m, portfolio.StopLossPercent);
        Assert.Equal(2, portfolio.Positions.Count);
        Assert.True(portfolio.Positions.All(p => p.Status == PositionStatus.Holding && p.BenchmarkValueUsdt == p.InitialValueUsdt));
    }

    [Fact]
    public async Task Without_admin_settings_only_roles_and_assets_are_seeded()
    {
        using var db = new TestDatabase(o => o.CreateDemoPortfolio = true);
        using (var scope = db.NewScope())
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();

        using var context = db.NewContext();
        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Equal(0, await context.Users.CountAsync());
        Assert.Equal(2, await context.Assets.CountAsync());
        Assert.Equal(0, await context.Portfolios.CountAsync());
    }

    [Fact]
    public async Task A_weak_admin_password_fails_loudly()
    {
        using var db = new TestDatabase(o => { o.AdminEmail = "admin@example.com"; o.AdminPassword = "123"; });
        using var scope = db.NewScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync());
    }

    [Fact]
    public async Task InitializeDatabaseAsync_migrates_and_seeds()
    {
        using var db = new TestDatabase(FullSeed);
        await db.Services.InitializeDatabaseAsync();
        using var context = db.NewContext();
        Assert.Equal(2, await context.Assets.CountAsync());
    }
}
