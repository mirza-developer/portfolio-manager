using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Abstractions;

public interface IAssetRepository
{
    /// <summary>All assets (enabled and disabled): disabled assets still need prices so held ones stay protected.</summary>
    Task<IReadOnlyList<Asset>> GetAllAsync(CancellationToken ct = default);
}

public interface IPortfolioRepository
{
    /// <summary>Portfolios to process, including their Positions. The engine re-checks IsAutoTradingEnabled itself.</summary>
    Task<IReadOnlyList<Portfolio>> GetAutoTradingPortfoliosAsync(CancellationToken ct = default);
}

public interface ITradeLogRepository
{
    /// <summary>Stages a log row; it is persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(TradeLog log);
}

public interface IPriceLogRepository
{
    void AddRange(IEnumerable<PriceLog> logs);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
