using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Tests.TestDoubles;

/// <summary>One in-memory object implementing every repository interface plus the unit of work.</summary>
public sealed class InMemoryStore : IAssetRepository, IPortfolioRepository, ITradeLogRepository, IPriceLogRepository, IUnitOfWork
{
    public List<Asset> Assets { get; } = new();
    public List<Portfolio> Portfolios { get; } = new();
    public List<TradeLog> TradeLogs { get; } = new();
    public List<PriceLog> PriceLogs { get; } = new();
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Asset>>(Assets.ToList());

    // Returns ALL portfolios on purpose: the engine must re-check IsAutoTradingEnabled itself.
    public Task<IReadOnlyList<Portfolio>> GetAutoTradingPortfoliosAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Portfolio>>(Portfolios.ToList());

    public void Add(TradeLog log) => TradeLogs.Add(log);

    public void AddRange(IEnumerable<PriceLog> logs) => PriceLogs.AddRange(logs);

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
