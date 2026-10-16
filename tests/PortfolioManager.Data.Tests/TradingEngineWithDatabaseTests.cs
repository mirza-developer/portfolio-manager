using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioManager.Core;
using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Engine;
using PortfolioManager.Core.Prices;

namespace PortfolioManager.Data.Tests;

/// <summary>The real Core engine running on the real EF repositories (SQLite), proving the two projects work together.</summary>
public class TradingEngineWithDatabaseTests
{
    private static TestDatabase NewDb(decimal goldPrice) => new(configureServices: services =>
    {
        services.AddSingleton<IPriceProvider>(new SimulatedPriceProvider(1, 0m, new Dictionary<string, decimal> { ["GOLD"] = goldPrice }));
        services.AddTradingCore(o => { o.FeePercent = 0m; o.SlippagePercent = 0m; });
        services.AddSimulatedExchange();
    });

    private static async Task<int> ArrangeHeldGoldAsync(TestDatabase db)
    {
        using var context = db.NewContext();
        var userId = await TestDatabase.AddUserAsync(context);
        var gold = new Asset { Symbol = "GOLD", Name = "Digital Gold", PriceSource = "simulated" };
        context.Assets.Add(gold);
        var portfolio = new Portfolio
        {
            UserId = userId, Name = "p", UsdtBalance = 0m, CreatedUtc = DateTime.UtcNow,
            Positions =
            {
                new Position { Asset = gold, Quantity = 10m, InitialValueUsdt = 1000m, BenchmarkValueUsdt = 1000m, Status = PositionStatus.Holding }
            }
        };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        return portfolio.Id;
    }

    [Fact]
    public async Task A_price_drop_triggers_the_stop_loss_and_everything_is_persisted()
    {
        using var db = NewDb(goldPrice: 90m);
        var portfolioId = await ArrangeHeldGoldAsync(db);

        using (var scope = db.NewScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ITradingEngine>().RunCycleAsync();
            Assert.Equal(1, result.PortfoliosProcessed);
            Assert.Equal(1, result.TradesExecuted);
        }

        using var read = db.NewContext();
        var portfolio = await read.Portfolios.Include(p => p.Positions).SingleAsync(p => p.Id == portfolioId);
        var position = Assert.Single(portfolio.Positions);
        Assert.Equal(900m, portfolio.UsdtBalance);
        Assert.Equal(PositionStatus.Cooldown, position.Status);
        Assert.Equal(0m, position.Quantity);
        Assert.Equal(90m, position.LastSellPriceUsdt);
        Assert.Equal(DateTimeKind.Utc, position.CooldownUntilUtc!.Value.Kind);

        var trade = await read.TradeLogs.SingleAsync();
        Assert.Equal(TradeSide.Sell, trade.Side);
        Assert.Equal(TradeReason.StopLoss, trade.Reason);
        Assert.Equal(900m, trade.ValueUsdt);
        Assert.Equal(1, await read.PriceLogs.CountAsync());
    }

    [Fact]
    public async Task A_price_rise_only_moves_the_benchmark_up()
    {
        using var db = NewDb(goldPrice: 120m);
        var portfolioId = await ArrangeHeldGoldAsync(db);

        using (var scope = db.NewScope())
            await scope.ServiceProvider.GetRequiredService<ITradingEngine>().RunCycleAsync();

        using var read = db.NewContext();
        var position = await read.Positions.SingleAsync(p => p.PortfolioId == portfolioId);
        Assert.Equal(1200m, position.BenchmarkValueUsdt);
        Assert.Equal(PositionStatus.Holding, position.Status);
        Assert.Equal(0, await read.TradeLogs.CountAsync());
    }

    [Fact]
    public async Task Auto_trading_off_leaves_the_database_untouched_except_for_price_logs()
    {
        using var db = NewDb(goldPrice: 50m);
        var portfolioId = await ArrangeHeldGoldAsync(db);
        using (var context = db.NewContext())
        {
            (await context.Portfolios.SingleAsync()).IsAutoTradingEnabled = false;
            await context.SaveChangesAsync();
        }

        using (var scope = db.NewScope())
            await scope.ServiceProvider.GetRequiredService<ITradingEngine>().RunCycleAsync();

        using var read = db.NewContext();
        Assert.Equal(PositionStatus.Holding, (await read.Positions.SingleAsync(p => p.PortfolioId == portfolioId)).Status);
        Assert.Equal(0, await read.TradeLogs.CountAsync());
        Assert.Equal(1, await read.PriceLogs.CountAsync());
    }
}
