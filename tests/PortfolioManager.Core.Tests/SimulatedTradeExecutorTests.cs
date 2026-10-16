using Microsoft.Extensions.Options;
using PortfolioManager.Core.Execution;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class SimulatedTradeExecutorTests
{
    private static SimulatedTradeExecutor Create(decimal fee, decimal slippage) =>
        new(Options.Create(new TradingOptions { FeePercent = fee, SlippagePercent = slippage }));

    [Fact]
    public async Task Buy_pays_the_fee_out_of_the_spent_amount()
    {
        var r = await Create(0.1m, 0m).BuyAsync(TestData.Gold(), 1000m, 100m);
        Assert.Equal(1m, r.FeeUsdt);
        Assert.Equal(100m, r.ExecutionPriceUsdt);
        Assert.Equal(9.99m, r.Quantity);
        Assert.Equal(1000m, r.ValueUsdt);
    }

    [Fact]
    public async Task Buy_slippage_raises_the_execution_price()
    {
        var r = await Create(0m, 1m).BuyAsync(TestData.Gold(), 1010m, 100m);
        Assert.Equal(101m, r.ExecutionPriceUsdt);
        Assert.Equal(10m, r.Quantity);
    }

    [Fact]
    public async Task Sell_slippage_lowers_the_price_and_the_fee_is_deducted_from_the_proceeds()
    {
        var r = await Create(0.1m, 0.05m).SellAsync(TestData.Gold(), 10m, 100m);
        Assert.Equal(99.95m, r.ExecutionPriceUsdt);
        Assert.Equal(0.9995m, r.FeeUsdt);
        Assert.Equal(998.5005m, r.ValueUsdt);
        Assert.Equal(10m, r.Quantity);
    }

    [Fact]
    public async Task Zero_fee_and_zero_slippage_trade_at_the_market_price()
    {
        var executor = Create(0m, 0m);
        var buy = await executor.BuyAsync(TestData.Gold(), 500m, 50m);
        Assert.Equal(10m, buy.Quantity);
        var sell = await executor.SellAsync(TestData.Gold(), 10m, 50m);
        Assert.Equal(500m, sell.ValueUsdt);
        Assert.Equal(0m, sell.FeeUsdt);
    }

    [Fact]
    public async Task Invalid_buy_arguments_throw()
    {
        var executor = Create(0m, 0m);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => executor.BuyAsync(TestData.Gold(), 0m, 100m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => executor.BuyAsync(TestData.Gold(), 100m, 0m));
    }

    [Fact]
    public async Task Invalid_sell_arguments_throw()
    {
        var executor = Create(0m, 0m);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => executor.SellAsync(TestData.Gold(), 0m, 100m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => executor.SellAsync(TestData.Gold(), 1m, 0m));
    }
}
