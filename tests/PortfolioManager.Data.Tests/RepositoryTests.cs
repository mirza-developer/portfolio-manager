using Microsoft.EntityFrameworkCore;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data.Repositories;

namespace PortfolioManager.Data.Tests;

public class RepositoryTests
{
    private static Asset NewAsset(string symbol, bool enabled = true) =>
        new() { Symbol = symbol, Name = symbol + " coin", IsEnabled = enabled, MinTradeSizeUsdt = 10m, PriceSource = "simulated" };

    [Fact]
    public async Task Asset_repository_returns_all_assets_including_disabled_ones_in_id_order()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        context.Assets.AddRange(NewAsset("GOLD"), NewAsset("SILVER", enabled: false));
        await context.SaveChangesAsync();

        var all = await new EfAssetRepository(context).GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("GOLD", all[0].Symbol);
        Assert.False(all[1].IsEnabled);
    }

    [Fact]
    public async Task Portfolio_repository_returns_only_auto_trading_portfolios_with_their_positions()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        var userId = await TestDatabase.AddUserAsync(context);
        var gold = NewAsset("GOLD");
        context.Assets.Add(gold);
        await context.SaveChangesAsync();
        context.Portfolios.AddRange(
            new Portfolio { UserId = userId, Name = "on", Positions = { new Position { AssetId = gold.Id, Quantity = 1m } } },
            new Portfolio { UserId = userId, Name = "off", IsAutoTradingEnabled = false });
        await context.SaveChangesAsync();

        using var readContext = db.NewContext();
        var result = await new EfPortfolioRepository(readContext).GetAutoTradingPortfoliosAsync();

        var only = Assert.Single(result);
        Assert.Equal("on", only.Name);
        Assert.Single(only.Positions);
    }

    [Fact]
    public async Task Decimals_dates_and_enums_round_trip_exactly()
    {
        using var db = new TestDatabase();
        var timestamp = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        int portfolioId;
        int assetId;
        using (var context = db.NewContext())
        {
            var userId = await TestDatabase.AddUserAsync(context);
            var gold = NewAsset("GOLD");
            context.Assets.Add(gold);
            var portfolio = new Portfolio
            {
                UserId = userId,
                Name = "p",
                UsdtBalance = 1234.5678901234m,
                CreatedUtc = timestamp,
                Positions =
                {
                    new Position
                    {
                        Asset = gold,
                        Quantity = 0.0000000001m,
                        BenchmarkValueUsdt = 999999.9999999999m,
                        StopLossPercentOverride = 7.5m,
                        Status = PositionStatus.Cooldown,
                        CooldownUntilUtc = timestamp,
                        LastSellPriceUsdt = 94.12345m
                    }
                }
            };
            context.Portfolios.Add(portfolio);
            await context.SaveChangesAsync();
            portfolioId = portfolio.Id;
            assetId = gold.Id;
            context.TradeLogs.Add(new TradeLog
            {
                PortfolioId = portfolioId, AssetId = assetId, Side = TradeSide.Sell, Reason = TradeReason.StopLoss,
                Quantity = 1m, PriceUsdt = 1m, ValueUsdt = 1234.5678901234m, TimestampUtc = timestamp
            });
            await context.SaveChangesAsync();
        }

        using var read = db.NewContext();
        var loaded = await read.Portfolios.Include(p => p.Positions).SingleAsync(p => p.Id == portfolioId);
        var position = Assert.Single(loaded.Positions);
        var log = await read.TradeLogs.SingleAsync();

        Assert.Equal(1234.5678901234m, loaded.UsdtBalance);
        Assert.Equal(timestamp, loaded.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedUtc.Kind);
        Assert.Equal(0.0000000001m, position.Quantity);
        Assert.Equal(999999.9999999999m, position.BenchmarkValueUsdt);
        Assert.Equal(7.5m, position.StopLossPercentOverride);
        Assert.Equal(PositionStatus.Cooldown, position.Status);
        Assert.Equal(timestamp, position.CooldownUntilUtc);
        Assert.Equal(DateTimeKind.Utc, position.CooldownUntilUtc!.Value.Kind);
        Assert.Equal(94.12345m, position.LastSellPriceUsdt);
        Assert.Equal(TradeSide.Sell, log.Side);
        Assert.Equal(1234.5678901234m, log.ValueUsdt);
    }

    [Fact]
    public async Task Asset_symbol_must_be_unique()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        context.Assets.AddRange(NewAsset("GOLD"), NewAsset("GOLD"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task A_portfolio_can_hold_an_asset_only_once()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        var userId = await TestDatabase.AddUserAsync(context);
        var gold = NewAsset("GOLD");
        context.Assets.Add(gold);
        await context.SaveChangesAsync();
        context.Portfolios.Add(new Portfolio
        {
            UserId = userId, Name = "p",
            Positions = { new Position { AssetId = gold.Id }, new Position { AssetId = gold.Id } }
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_a_portfolio_deletes_its_positions()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        var userId = await TestDatabase.AddUserAsync(context);
        var gold = NewAsset("GOLD");
        var silver = NewAsset("SILVER");
        context.Assets.AddRange(gold, silver);
        await context.SaveChangesAsync();
        var portfolio = new Portfolio
        {
            UserId = userId, Name = "p",
            Positions = { new Position { AssetId = gold.Id }, new Position { AssetId = silver.Id } }
        };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        Assert.Equal(2, await context.Positions.CountAsync());

        context.Portfolios.Remove(portfolio);
        await context.SaveChangesAsync();

        Assert.Equal(0, await context.Positions.CountAsync());
    }

    [Fact]
    public async Task A_portfolio_with_trade_logs_cannot_be_deleted()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        var userId = await TestDatabase.AddUserAsync(context);
        var gold = NewAsset("GOLD");
        context.Assets.Add(gold);
        var portfolio = new Portfolio { UserId = userId, Name = "p" };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        context.TradeLogs.Add(new TradeLog { PortfolioId = portfolio.Id, AssetId = gold.Id, Side = TradeSide.Buy, Reason = TradeReason.Initial, TimestampUtc = DateTime.UtcNow });
        await context.SaveChangesAsync();

        context.Portfolios.Remove(portfolio);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Trade_and_price_log_repositories_stage_rows_that_the_unit_of_work_saves()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        var userId = await TestDatabase.AddUserAsync(context);
        var gold = NewAsset("GOLD");
        context.Assets.Add(gold);
        var portfolio = new Portfolio { UserId = userId, Name = "p" };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();

        var trades = new EfTradeLogRepository(context);
        var prices = new EfPriceLogRepository(context);
        trades.Add(new TradeLog { PortfolioId = portfolio.Id, AssetId = gold.Id, Side = TradeSide.Buy, Reason = TradeReason.Initial, TimestampUtc = DateTime.UtcNow });
        prices.AddRange(new[]
        {
            new PriceLog { AssetId = gold.Id, PriceUsdt = 100m, TimestampUtc = DateTime.UtcNow, Source = "simulated" },
            new PriceLog { AssetId = gold.Id, PriceUsdt = 101m, TimestampUtc = DateTime.UtcNow, Source = "simulated" }
        });
        Assert.Equal(0, await db.NewContext().TradeLogs.CountAsync()); // nothing saved yet

        await new EfUnitOfWork(context).SaveChangesAsync();

        using var read = db.NewContext();
        Assert.Equal(1, await read.TradeLogs.CountAsync());
        Assert.Equal(2, await read.PriceLogs.CountAsync());
    }

    [Fact]
    public async Task A_failed_save_discards_added_rows_and_restores_modified_ones_so_the_next_save_works()
    {
        using var db = new TestDatabase();
        using var context = db.NewContext();
        var unitOfWork = new EfUnitOfWork(context);
        var gold = NewAsset("GOLD");
        context.Assets.Add(gold);
        await unitOfWork.SaveChangesAsync();

        gold.Name = "changed";
        context.Assets.Add(NewAsset("GOLD")); // duplicate symbol: the save must fail
        await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync());

        Assert.Equal("GOLD coin", gold.Name);
        Assert.Empty(context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added));

        context.Assets.Add(NewAsset("SILVER"));
        await unitOfWork.SaveChangesAsync();
        Assert.Equal(2, await context.Assets.CountAsync());
    }
}
