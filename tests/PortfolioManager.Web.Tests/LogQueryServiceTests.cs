using Microsoft.Extensions.DependencyInjection;
using PortfolioManager.Core.Domain;
using PortfolioManager.Web.Services;

namespace PortfolioManager.Web.Tests;

public class LogQueryServiceTests
{
    private static ILogQueryService Service(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ILogQueryService>();

    private static readonly DateTime Day0 = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Trade_logs_are_paged_newest_first()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        var pid = await db.AddPortfolioAsync(root.UserId, "p", 0m);
        for (var i = 0; i < 25; i++) await db.AddTradeAsync(pid, 1, TradeSide.Sell, Day0.AddHours(i));

        using var scope = db.NewScope();
        var first = await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter(), 0, 10);
        var last = await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter(), 2, 10);

        Assert.Equal(25, first.TotalCount);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(Day0.AddHours(24), first.Items[0].TimestampUtc);
        Assert.Equal(5, last.Items.Count);
        Assert.Equal(Day0, last.Items[^1].TimestampUtc);
        Assert.Equal("p", first.Items[0].PortfolioName);
        Assert.Equal("GOLD", first.Items[0].Symbol);
    }

    [Fact]
    public async Task A_regular_user_sees_only_the_logs_of_their_own_portfolios()
    {
        using var db = new WebTestDatabase();
        var alice = await db.AddUserAsync("alice@example.com", false);
        var bob = await db.AddUserAsync("bob@example.com", false);
        var root = await db.AddUserAsync("root@example.com", true);
        var a = await db.AddPortfolioAsync(alice.UserId, "a", 0m);
        var b = await db.AddPortfolioAsync(bob.UserId, "b", 0m);
        await db.AddTradeAsync(a, 1, TradeSide.Buy, Day0);
        await db.AddTradeAsync(b, 1, TradeSide.Buy, Day0);
        await db.AddTradeAsync(b, 2, TradeSide.Sell, Day0);

        using var scope = db.NewScope();
        Assert.Equal(1, (await Service(scope).GetTradeLogsAsync(alice, new TradeLogFilter(), 0, 50)).TotalCount);
        Assert.Equal(2, (await Service(scope).GetTradeLogsAsync(bob, new TradeLogFilter(), 0, 50)).TotalCount);
        Assert.Equal(3, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter(), 0, 50)).TotalCount);

        // asking for someone else's portfolio through the filter still returns nothing
        Assert.Equal(0, (await Service(scope).GetTradeLogsAsync(alice, new TradeLogFilter { PortfolioId = b }, 0, 50)).TotalCount);
    }

    [Fact]
    public async Task Trade_logs_can_be_filtered_by_asset_side_reason_and_date_with_an_exclusive_end()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        var pid = await db.AddPortfolioAsync(root.UserId, "p", 0m);
        await db.AddTradeAsync(pid, 1, TradeSide.Sell, Day0, TradeReason.StopLoss);
        await db.AddTradeAsync(pid, 2, TradeSide.Buy, Day0.AddDays(1), TradeReason.Initial);
        await db.AddTradeAsync(pid, 2, TradeSide.Buy, Day0.AddDays(2), TradeReason.Reinvest);

        using var scope = db.NewScope();
        Assert.Equal(1, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter { AssetId = 1 }, 0, 50)).TotalCount);
        Assert.Equal(2, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter { Side = TradeSide.Buy }, 0, 50)).TotalCount);
        Assert.Equal(1, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter { Reason = TradeReason.Reinvest }, 0, 50)).TotalCount);
        Assert.Equal(2, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter { FromUtc = Day0.AddDays(1) }, 0, 50)).TotalCount);
        // 'to' is exclusive: a log exactly at the end is not included
        Assert.Equal(1, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter { ToUtc = Day0.AddDays(1) }, 0, 50)).TotalCount);
        Assert.Equal(0, (await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter { PortfolioId = pid, Side = TradeSide.Sell, AssetId = 2 }, 0, 50)).TotalCount);
    }

    [Fact]
    public async Task Page_arguments_are_clamped()
    {
        using var db = new WebTestDatabase();
        var root = await db.AddUserAsync("root@example.com", true);
        var pid = await db.AddPortfolioAsync(root.UserId, "p", 0m);
        for (var i = 0; i < 3; i++) await db.AddTradeAsync(pid, 1, TradeSide.Buy, Day0.AddHours(i));

        using var scope = db.NewScope();
        var result = await Service(scope).GetTradeLogsAsync(root, new TradeLogFilter(), -5, 0);
        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Price_logs_can_be_filtered_by_asset_and_date_and_are_paged()
    {
        using var db = new WebTestDatabase();
        for (var i = 0; i < 12; i++) await db.AddPriceAsync(1, 100m + i, Day0.AddHours(i));
        await db.AddPriceAsync(2, 10m, Day0);

        using var scope = db.NewScope();
        var gold = await Service(scope).GetPriceLogsAsync(new PriceLogFilter { AssetId = 1 }, 0, 5);
        Assert.Equal(12, gold.TotalCount);
        Assert.Equal(5, gold.Items.Count);
        Assert.Equal(111m, gold.Items[0].PriceUsdt);          // newest first
        Assert.Equal("GOLD", gold.Items[0].Symbol);

        Assert.Equal(13, (await Service(scope).GetPriceLogsAsync(new PriceLogFilter(), 0, 50)).TotalCount);
        Assert.Equal(2, (await Service(scope).GetPriceLogsAsync(new PriceLogFilter { AssetId = 1, FromUtc = Day0.AddHours(10) }, 0, 50)).TotalCount);
        Assert.Equal(1, (await Service(scope).GetPriceLogsAsync(new PriceLogFilter { AssetId = 1, ToUtc = Day0.AddHours(1) }, 0, 50)).TotalCount);
    }
}
