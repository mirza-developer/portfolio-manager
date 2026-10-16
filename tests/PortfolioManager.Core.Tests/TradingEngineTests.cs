using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Strategy;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class TradingEngineTests
{
    private static EngineEnv NewEnv(params Asset[] assets)
    {
        var env = new EngineEnv();
        env.Store.Assets.AddRange(assets);
        return env;
    }

    /// <summary>A strategy that asks for far too much, to prove the guard has the last word.</summary>
    private sealed class GreedyStrategy : IBuyStrategy
    {
        public IReadOnlyList<PlannedBuy> Plan(BuyContext context) =>
            context.Candidates.Select(c => new PlannedBuy(c.Asset, 1_000_000m)).ToList();
    }

    // ---------- protection ----------

    [Fact]
    public async Task Stop_loss_sells_everything_into_usdt_logs_the_trade_and_starts_the_cooldown()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 94m;

        var result = await env.RunAsync();

        var pos = Assert.Single(p.Positions);
        Assert.Equal(PositionStatus.Cooldown, pos.Status);
        Assert.Equal(0m, pos.Quantity);
        Assert.Equal(940m, p.UsdtBalance);
        Assert.Equal(TestData.T0.AddHours(4), pos.CooldownUntilUtc);
        Assert.Equal(94m, pos.LastSellPriceUsdt);

        var log = Assert.Single(env.Store.TradeLogs);
        Assert.Equal(TradeSide.Sell, log.Side);
        Assert.Equal(TradeReason.StopLoss, log.Reason);
        Assert.Equal(10m, log.Quantity);
        Assert.Equal(940m, log.ValueUsdt);
        Assert.Equal(1, log.PortfolioId);
        Assert.Equal(1, log.AssetId);
        Assert.Equal(1, result.PortfoliosProcessed);
        Assert.Equal(1, result.TradesExecuted);
    }

    [Fact]
    public async Task A_loss_exactly_at_the_limit_is_not_sold()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 95m; // value 950 = benchmark x 0.95

        await env.RunAsync();

        Assert.Equal(PositionStatus.Holding, p.Positions[0].Status);
        Assert.Empty(env.Store.TradeLogs);
    }

    [Fact]
    public async Task A_price_rise_raises_the_benchmark_without_trading()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 50m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 120m;

        await env.RunAsync();

        Assert.Equal(1200m, p.Positions[0].BenchmarkValueUsdt);
        Assert.Equal(10m, p.Positions[0].Quantity);
        Assert.Equal(50m, p.UsdtBalance);
        Assert.Empty(env.Store.TradeLogs);
    }

    [Fact]
    public async Task The_trailing_benchmark_protects_profit_not_only_the_initial_value()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);

        env.Prices.Prices["GOLD"] = 130m; // benchmark 1300
        await env.RunAsync();
        env.Prices.Prices["GOLD"] = 124m; // 1240 is still above the 1235 line, and far above the initial 1000
        await env.RunAsync();
        Assert.Equal(PositionStatus.Holding, p.Positions[0].Status);

        env.Prices.Prices["GOLD"] = 123m; // 1230 < 1235 -> sell, locking in the profit
        await env.RunAsync();
        Assert.Equal(PositionStatus.Cooldown, p.Positions[0].Status);
        Assert.Equal(1230m, p.UsdtBalance);
    }

    [Fact]
    public async Task A_position_override_beats_the_portfolio_limit_in_both_directions()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        var p = TestData.NewPortfolio(1, 0m,
            TestData.Holding(1, 10m, 1000m, stopOverride: 20m),   // looser than the portfolio's 5%
            TestData.Holding(2, 10m, 1000m, stopOverride: 2m));   // stricter than the portfolio's 5%
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 90m;    // -10%: kept because the limit is 20%
        env.Prices.Prices["SILVER"] = 97m;  // -3%: sold because the limit is 2%

        await env.RunAsync();

        Assert.Equal(PositionStatus.Holding, p.Positions[0].Status);
        Assert.Equal(PositionStatus.Cooldown, p.Positions[1].Status);
    }

    [Fact]
    public async Task Stop_loss_runs_before_buys_and_a_just_sold_asset_is_not_rebought_in_the_same_cycle()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        var p = TestData.NewPortfolio(1, 500m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 90m;   // value 900 < 950 -> sold, USDT becomes 1400
        env.Prices.Prices["SILVER"] = 10m;

        await env.RunAsync();

        var gold = p.Positions.Single(x => x.AssetId == 1);
        var silver = p.Positions.Single(x => x.AssetId == 2);
        Assert.Equal(PositionStatus.Cooldown, gold.Status);
        Assert.Equal(0m, gold.Quantity);
        // investable = 1400 - 10% reserve = 1260, capped by 40% max allocation = 560
        Assert.Equal(56m, silver.Quantity);
        Assert.Equal(560m, silver.BenchmarkValueUsdt);
        Assert.Equal(840m, p.UsdtBalance);
        Assert.Equal(2, env.Store.TradeLogs.Count);
        Assert.Equal(TradeSide.Sell, env.Store.TradeLogs[0].Side);
        Assert.Equal(TradeSide.Buy, env.Store.TradeLogs[1].Side);
    }

    [Fact]
    public async Task A_disabled_asset_that_is_held_is_still_protected()
    {
        var env = NewEnv(TestData.Gold(enabled: false));
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 90m;

        await env.RunAsync();

        Assert.Equal(PositionStatus.Cooldown, p.Positions[0].Status);
        Assert.Equal(900m, p.UsdtBalance);
    }

    // ---------- compounding / re-entry ----------

    [Fact]
    public async Task After_cooldown_and_recovery_all_usdt_including_profit_is_reinvested_with_a_fresh_benchmark()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 1230m, TestData.InCooldown(1, 123m, TestData.T0.AddHours(4)));
        p.MaxAllocationPercent = 100m;
        p.CashReservePercent = 0m;
        env.Store.Portfolios.Add(p);
        env.Clock.Advance(TimeSpan.FromHours(4));
        env.Prices.Prices["GOLD"] = 125m; // >= 123 x 1.01

        await env.RunAsync();

        var pos = Assert.Single(p.Positions);
        Assert.Equal(PositionStatus.Holding, pos.Status);
        Assert.Equal(9.84m, pos.Quantity);
        Assert.Equal(1230m, pos.BenchmarkValueUsdt);
        Assert.Null(pos.CooldownUntilUtc);
        Assert.Equal(0m, p.UsdtBalance);
        var log = Assert.Single(env.Store.TradeLogs);
        Assert.Equal(TradeReason.Reinvest, log.Reason);
        Assert.Equal(TradeSide.Buy, log.Side);
    }

    [Fact]
    public async Task No_rebuy_while_the_cooldown_is_running()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 1000m, TestData.InCooldown(1, 100m, TestData.T0.AddHours(4)));
        p.MaxAllocationPercent = 100m;
        env.Store.Portfolios.Add(p);
        env.Clock.Advance(TimeSpan.FromHours(3));
        env.Prices.Prices["GOLD"] = 200m;

        await env.RunAsync();

        Assert.Equal(PositionStatus.Cooldown, p.Positions[0].Status);
        Assert.Empty(env.Store.TradeLogs);
    }

    [Fact]
    public async Task No_rebuy_until_the_price_has_recovered()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 1000m, TestData.InCooldown(1, 100m, TestData.T0.AddHours(4)));
        p.MaxAllocationPercent = 100m;
        env.Store.Portfolios.Add(p);
        env.Clock.Advance(TimeSpan.FromHours(5));
        env.Prices.Prices["GOLD"] = 100.5m; // needs 101

        await env.RunAsync();
        Assert.Empty(env.Store.TradeLogs);

        env.Prices.Prices["GOLD"] = 101m;
        await env.RunAsync();
        Assert.Single(env.Store.TradeLogs);
    }

    [Fact]
    public async Task The_cash_reserve_stays_in_usdt()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 1000m);
        p.MaxAllocationPercent = 100m; // reserve 10%
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 100m;

        await env.RunAsync();

        Assert.Equal(100m, p.UsdtBalance);
        Assert.Equal(9m, p.Positions.Single().Quantity);
        Assert.Equal(TradeReason.Initial, env.Store.TradeLogs.Single().Reason);
    }

    [Fact]
    public async Task The_guard_has_the_last_word_over_a_greedy_strategy()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 1000m); // max allocation 40%, reserve 10%
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 100m;

        await env.Create(new GreedyStrategy()).RunCycleAsync();

        Assert.Equal(4m, p.Positions.Single().Quantity); // 400 USDT = 40% of 1000
        Assert.Equal(600m, p.UsdtBalance);
    }

    [Fact]
    public async Task A_new_asset_added_as_data_is_traded_without_any_code_change()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        var p = TestData.NewPortfolio(1, 1000m);
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 100m;
        env.Prices.Prices["SILVER"] = 10m;
        env.Prices.Prices["DOGE"] = 0.5m;
        env.Store.Assets.Add(TestData.Doge());   // <- the only "change": a new asset row

        await env.RunAsync();

        Assert.Equal(3, p.Positions.Count);
        Assert.Equal(3m, p.Positions.Single(x => x.AssetId == 1).Quantity);
        Assert.Equal(30m, p.Positions.Single(x => x.AssetId == 2).Quantity);
        Assert.Equal(600m, p.Positions.Single(x => x.AssetId == 3).Quantity);
        Assert.Equal(100m, p.UsdtBalance);
    }

    [Fact]
    public async Task A_disabled_asset_is_never_bought()
    {
        var env = NewEnv(TestData.Silver(enabled: false));
        var p = TestData.NewPortfolio(1, 1000m);
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["SILVER"] = 10m;

        await env.RunAsync();

        Assert.Empty(p.Positions);
        Assert.Equal(1000m, p.UsdtBalance);
    }

    // ---------- skipping and isolation ----------

    [Fact]
    public async Task Auto_trading_off_skips_the_whole_portfolio()
    {
        var env = NewEnv(TestData.Gold());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        p.IsAutoTradingEnabled = false;
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 50m;

        var result = await env.RunAsync();

        Assert.Equal(PositionStatus.Holding, p.Positions[0].Status);
        Assert.Equal(10m, p.Positions[0].Quantity);
        Assert.Empty(env.Store.TradeLogs);
        Assert.Equal(0, result.PortfoliosProcessed);
    }

    [Fact]
    public async Task A_missing_price_skips_only_that_asset_and_blocks_buys_for_the_portfolio()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m), TestData.Holding(2, 100m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["SILVER"] = 9m; // GOLD has no price at all

        await env.RunAsync();

        Assert.Equal(PositionStatus.Holding, p.Positions[0].Status);  // GOLD untouched
        Assert.Equal(10m, p.Positions[0].Quantity);
        Assert.Equal(PositionStatus.Cooldown, p.Positions[1].Status); // SILVER still protected
        Assert.Equal(900m, p.UsdtBalance);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task An_invalid_price_for_a_held_asset_blocks_new_buys(int badPrice)
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        var p = TestData.NewPortfolio(1, 1000m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = badPrice;
        env.Prices.Prices["SILVER"] = 10m;

        await env.RunAsync();

        Assert.Single(p.Positions);
        Assert.Equal(1000m, p.UsdtBalance);
        Assert.Empty(env.Store.TradeLogs);
    }

    [Fact]
    public async Task A_failing_portfolio_applies_nothing_and_does_not_stop_the_others()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        // Portfolio 1: GOLD would ratchet its benchmark first, then SILVER has an invalid override (0) and throws.
        var bad = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m), TestData.Holding(2, 10m, 1000m, stopOverride: 0m));
        var good = TestData.NewPortfolio(2, 0m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.AddRange(new[] { bad, good });
        env.Prices.Prices["GOLD"] = 120m;
        env.Prices.Prices["SILVER"] = 100m;

        var result = await env.RunAsync();

        Assert.Equal(1000m, bad.Positions[0].BenchmarkValueUsdt);   // rolled back: untouched
        Assert.Equal(1200m, good.Positions[0].BenchmarkValueUsdt);  // processed normally
        Assert.Equal(1, result.PortfoliosFailed);
        Assert.Equal(1, result.PortfoliosProcessed);
    }

    // ---------- logs, costs, scenario ----------

    [Fact]
    public async Task Prices_are_logged_every_cycle_only_for_assets_with_a_valid_price()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver(), TestData.Doge());
        env.Prices.Prices["GOLD"] = 100m;
        env.Prices.Prices["SILVER"] = 10m; // DOGE has no price

        await env.RunAsync();
        env.Clock.Advance(TimeSpan.FromHours(1));
        await env.RunAsync();

        Assert.Equal(4, env.Store.PriceLogs.Count);
        Assert.True(env.Store.PriceLogs.All(l => l.AssetId != 3));
        Assert.Equal(TestData.T0, env.Store.PriceLogs[0].TimestampUtc);
        Assert.Equal(TestData.T0.AddHours(1), env.Store.PriceLogs[3].TimestampUtc);
        Assert.Equal("simulated", env.Store.PriceLogs[0].Source);
        Assert.True(env.Store.SaveCount >= 2);
    }

    [Fact]
    public async Task Fees_and_slippage_are_applied_to_a_stop_loss_sale()
    {
        var env = NewEnv(TestData.Gold());
        env.Opts.FeePercent = 0.1m;
        env.Opts.SlippagePercent = 0.05m;
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m));
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 90m;

        await env.RunAsync();

        var log = Assert.Single(env.Store.TradeLogs);
        Assert.Equal(89.955m, log.PriceUsdt);
        Assert.Equal(0.89955m, log.FeeUsdt);
        Assert.Equal(898.65045m, log.ValueUsdt);
        Assert.Equal(898.65045m, p.UsdtBalance);
        Assert.Equal(90m, p.Positions[0].LastSellPriceUsdt);
    }

    [Fact]
    public async Task Fees_are_applied_to_a_buy()
    {
        var env = NewEnv(TestData.Gold());
        env.Opts.FeePercent = 0.1m;
        var p = TestData.NewPortfolio(1, 1000m);
        p.MaxAllocationPercent = 100m;
        p.CashReservePercent = 0m;
        env.Store.Portfolios.Add(p);
        env.Prices.Prices["GOLD"] = 100m;

        await env.RunAsync();

        Assert.Equal(9.99m, p.Positions.Single().Quantity);
        Assert.Equal(999m, p.Positions.Single().BenchmarkValueUsdt);
        Assert.Equal(1m, env.Store.TradeLogs.Single().FeeUsdt);
        Assert.Equal(0m, p.UsdtBalance);
    }

    [Fact]
    public async Task Multi_cycle_scenario_gold_silver_and_usdt_with_compounding()
    {
        var env = NewEnv(TestData.Gold(), TestData.Silver());
        var p = TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m), TestData.Holding(2, 100m, 1000m));
        p.MaxAllocationPercent = 100m;
        p.CashReservePercent = 0m;
        p.CooldownHours = 1;
        env.Store.Portfolios.Add(p);
        var gold = p.Positions[0];
        var silver = p.Positions[1];

        async Task Cycle(decimal goldPrice, decimal silverPrice)
        {
            env.Prices.Prices["GOLD"] = goldPrice;
            env.Prices.Prices["SILVER"] = silverPrice;
            await env.RunAsync();
            env.Clock.Advance(TimeSpan.FromHours(1));
        }

        await Cycle(100m, 10m);  // t0: nothing happens
        Assert.Empty(env.Store.TradeLogs);

        await Cycle(130m, 10m);  // t1: gold benchmark ratchets to 1300
        Assert.Equal(1300m, gold.BenchmarkValueUsdt);

        await Cycle(123m, 10m);  // t2: 1230 < 1235 -> gold sold, 1230 USDT locked in
        Assert.Equal(PositionStatus.Cooldown, gold.Status);
        Assert.Equal(1230m, p.UsdtBalance);

        await Cycle(125m, 10m);  // t3: cooldown over and 125 >= 123 x 1.01 -> all 1230 USDT reinvested
        Assert.Equal(PositionStatus.Holding, gold.Status);
        Assert.Equal(9.84m, gold.Quantity);
        Assert.Equal(1230m, gold.BenchmarkValueUsdt);
        Assert.Equal(0m, p.UsdtBalance);

        await Cycle(150m, 8m);   // t4: gold benchmark 1476; silver 800 < 950 -> sold
        Assert.Equal(1476m, gold.BenchmarkValueUsdt);
        Assert.Equal(PositionStatus.Cooldown, silver.Status);
        Assert.Equal(800m, p.UsdtBalance);

        Assert.Equal(3, env.Store.TradeLogs.Count);
        Assert.Equal(2276m, p.UsdtBalance + gold.Quantity * 150m); // started at 2000
    }

    [Fact]
    public async Task The_cycle_result_counts_processed_failed_and_trades()
    {
        var env = NewEnv(TestData.Gold());
        env.Store.Portfolios.Add(TestData.NewPortfolio(1, 0m, TestData.Holding(1, 10m, 1000m)));          // sells
        var off = TestData.NewPortfolio(2, 0m, TestData.Holding(1, 10m, 1000m));
        off.IsAutoTradingEnabled = false;
        env.Store.Portfolios.Add(off);                                                                    // skipped
        env.Prices.Prices["GOLD"] = 90m;

        var result = await env.RunAsync();

        Assert.Equal(TestData.T0, result.StartedUtc);
        Assert.Equal(1, result.PortfoliosProcessed);
        Assert.Equal(0, result.PortfoliosFailed);
        Assert.Equal(1, result.TradesExecuted);
    }

    [Fact]
    public async Task A_failing_price_provider_stops_the_cycle_with_its_exception()
    {
        var env = NewEnv(TestData.Gold());
        env.Prices.ThrowOnGet = new InvalidOperationException("exchange down");
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.RunAsync());
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_cycle()
    {
        var env = NewEnv(TestData.Gold());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => env.RunAsync(cts.Token));
    }
}
