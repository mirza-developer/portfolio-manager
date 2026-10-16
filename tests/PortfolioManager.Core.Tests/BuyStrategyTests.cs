using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Strategy;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class BuyStrategyTests
{
    private static BuyCandidate Cand(Asset asset, decimal price = 100m, decimal current = 0m) => new(asset, price, current);

    private static BuyContext Ctx(Portfolio p, decimal investable, decimal value, params BuyCandidate[] candidates) =>
        new(p, investable, value, candidates);

    [Fact]
    public void Equal_weight_splits_the_investable_amount_equally()
    {
        var ctx = Ctx(TestData.NewPortfolio(), 900m, 1000m, Cand(TestData.Gold()), Cand(TestData.Silver()), Cand(TestData.Doge()));
        var plan = new EqualWeightBuyStrategy().Plan(ctx);
        Assert.Equal(3, plan.Count);
        Assert.True(plan.All(p => p.SpendUsdt == 300m));
    }

    [Fact]
    public void Equal_weight_returns_nothing_without_candidates()
    {
        Assert.Empty(new EqualWeightBuyStrategy().Plan(Ctx(TestData.NewPortfolio(), 900m, 1000m)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Equal_weight_returns_nothing_when_there_is_nothing_to_invest(int investable)
    {
        var ctx = Ctx(TestData.NewPortfolio(), investable, 1000m, Cand(TestData.Gold()));
        Assert.Empty(new EqualWeightBuyStrategy().Plan(ctx));
    }

    [Fact]
    public void Guard_caps_a_buy_at_the_max_allocation_of_the_portfolio_value()
    {
        var p = TestData.NewPortfolio(); // max allocation 40%
        var ctx = Ctx(p, 900m, 1000m, Cand(TestData.Gold()));
        var result = new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 600m) });
        Assert.Equal(400m, Assert.Single(result).SpendUsdt);
    }

    [Fact]
    public void Guard_counts_the_value_already_held_in_the_asset()
    {
        var ctx = Ctx(TestData.NewPortfolio(), 900m, 1000m, Cand(TestData.Gold(), current: 100m));
        var result = new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 600m) });
        Assert.Equal(300m, Assert.Single(result).SpendUsdt);
    }

    [Fact]
    public void Guard_drops_a_buy_when_the_asset_is_already_at_its_cap()
    {
        var ctx = Ctx(TestData.NewPortfolio(), 900m, 1000m, Cand(TestData.Gold(), current: 400m));
        Assert.Empty(new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 100m) }));
    }

    [Fact]
    public void Guard_drops_buys_below_the_minimum_trade_size_and_keeps_the_exact_minimum()
    {
        var gold = TestData.Gold();
        gold.MinTradeSizeUsdt = 50m;
        var ctx = Ctx(TestData.NewPortfolio(), 900m, 1000m, Cand(gold));
        var guard = new TradeRuleGuard();
        Assert.Empty(guard.Apply(ctx, new[] { new PlannedBuy(gold, 30m) }));
        Assert.Single(guard.Apply(ctx, new[] { new PlannedBuy(gold, 50m) }));
    }

    [Fact]
    public void Guard_scales_the_total_down_to_the_investable_amount()
    {
        var p = TestData.NewPortfolio();
        p.MaxAllocationPercent = 100m;
        var ctx = Ctx(p, 600m, 2000m, Cand(TestData.Gold()), Cand(TestData.Silver()));
        var result = new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 600m), new PlannedBuy(TestData.Silver(), 600m) });
        Assert.Equal(2, result.Count);
        Assert.True(result.All(r => r.SpendUsdt == 300m));
    }

    [Fact]
    public void Guard_ignores_assets_that_are_not_candidates()
    {
        var ctx = Ctx(TestData.NewPortfolio(), 900m, 1000m, Cand(TestData.Gold()));
        Assert.Empty(new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Silver(), 100m) }));
    }

    [Fact]
    public void Guard_keeps_only_the_first_plan_per_asset()
    {
        var p = TestData.NewPortfolio();
        p.MaxAllocationPercent = 100m;
        var ctx = Ctx(p, 900m, 1000m, Cand(TestData.Gold()));
        var result = new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 100m), new PlannedBuy(TestData.Gold(), 500m) });
        Assert.Equal(100m, Assert.Single(result).SpendUsdt);
    }

    [Fact]
    public void Guard_leaves_a_plan_that_follows_the_rules_unchanged()
    {
        var p = TestData.NewPortfolio();
        p.MaxAllocationPercent = 100m;
        var ctx = Ctx(p, 500m, 1000m, Cand(TestData.Gold()), Cand(TestData.Silver()));
        var result = new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 100m), new PlannedBuy(TestData.Silver(), 100m) });
        Assert.Equal(2, result.Count);
        Assert.True(result.All(r => r.SpendUsdt == 100m));
    }

    [Fact]
    public void Guard_drops_non_positive_plans()
    {
        var ctx = Ctx(TestData.NewPortfolio(), 900m, 1000m, Cand(TestData.Gold()));
        Assert.Empty(new TradeRuleGuard().Apply(ctx, new[] { new PlannedBuy(TestData.Gold(), 0m) }));
    }
}
