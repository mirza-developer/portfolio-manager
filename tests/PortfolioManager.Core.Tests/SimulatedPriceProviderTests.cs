using PortfolioManager.Core.Domain;
using PortfolioManager.Core.Prices;
using PortfolioManager.Core.Tests.TestDoubles;

namespace PortfolioManager.Core.Tests;

public class SimulatedPriceProviderTests
{
    private static readonly IReadOnlyCollection<Asset> Assets = new[] { TestData.Gold(), TestData.Silver() };

    [Fact]
    public async Task First_call_returns_the_start_prices()
    {
        var provider = new SimulatedPriceProvider(1, 1m, new Dictionary<string, decimal> { ["GOLD"] = 250m });
        var prices = await provider.GetPricesAsync(Assets);
        Assert.Equal(250m, prices["GOLD"]);
        Assert.Equal(100m, prices["SILVER"]); // default start price
    }

    [Fact]
    public async Task Same_seed_gives_the_same_prices()
    {
        var a = new SimulatedPriceProvider(7, 2m);
        var b = new SimulatedPriceProvider(7, 2m);
        for (var i = 0; i < 20; i++)
        {
            var pa = await a.GetPricesAsync(Assets);
            var pb = await b.GetPricesAsync(Assets);
            Assert.Equal(pa["GOLD"], pb["GOLD"]);
            Assert.Equal(pa["SILVER"], pb["SILVER"]);
        }
    }

    [Fact]
    public async Task Different_seeds_give_different_prices()
    {
        var a = new SimulatedPriceProvider(1, 5m);
        var b = new SimulatedPriceProvider(2, 5m);
        await a.GetPricesAsync(Assets);
        await b.GetPricesAsync(Assets);
        var pa = await a.GetPricesAsync(Assets);
        var pb = await b.GetPricesAsync(Assets);
        Assert.NotEqual(pa["GOLD"], pb["GOLD"]);
    }

    [Fact]
    public async Task Prices_stay_positive_even_with_extreme_volatility()
    {
        var provider = new SimulatedPriceProvider(3, 100m, null, 0.00000002m);
        for (var i = 0; i < 1000; i++)
        {
            var prices = await provider.GetPricesAsync(Assets);
            Assert.True(prices.Values.All(p => p > 0m));
        }
    }

    [Fact]
    public async Task Price_moves_stay_within_the_volatility()
    {
        var provider = new SimulatedPriceProvider(5, 1m);
        var last = (await provider.GetPricesAsync(Assets))["GOLD"];
        for (var i = 0; i < 200; i++)
        {
            var next = (await provider.GetPricesAsync(Assets))["GOLD"];
            Assert.True(Math.Abs(next - last) / last <= 0.0101m);
            last = next;
        }
    }

    [Fact]
    public async Task Only_the_requested_assets_are_returned_and_symbols_are_case_insensitive()
    {
        var provider = new SimulatedPriceProvider();
        var prices = await provider.GetPricesAsync(new[] { TestData.Gold() });
        Assert.Single(prices);
        Assert.True(prices.ContainsKey("gold"));
    }
}
