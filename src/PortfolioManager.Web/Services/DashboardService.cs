using PortfolioManager.Core.Domain;
using PortfolioManager.Web.Security;

namespace PortfolioManager.Web.Services;

public interface IDashboardService
{
    Task<DashboardData> GetAsync(UserContext user, CancellationToken ct = default);
}

public sealed class DashboardService(IPortfolioService portfolios, ILogQueryService logs) : IDashboardService
{
    public async Task<DashboardData> GetAsync(UserContext user, CancellationToken ct = default)
    {
        var list = await portfolios.ListAsync(user, ct);
        var latest = await logs.GetTradeLogsAsync(user, new TradeLogFilter(), 0, 10, ct);
        return new DashboardData(
            list.Sum(p => p.TotalValueUsdt),
            list.Sum(p => p.UsdtBalance),
            list.Sum(p => p.HoldingCount),
            latest.Items.Count > 0 ? latest.Items[0].TimestampUtc : null,
            latest.Items);
    }
}
