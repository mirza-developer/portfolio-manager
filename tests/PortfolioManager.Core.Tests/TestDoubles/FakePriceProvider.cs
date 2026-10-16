using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Tests.TestDoubles;

public sealed class FakePriceProvider : IPriceProvider
{
    public Dictionary<string, decimal> Prices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Exception? ThrowOnGet { get; set; }

    public Task<IReadOnlyDictionary<string, decimal>> GetPricesAsync(IReadOnlyCollection<Asset> assets, CancellationToken ct = default)
    {
        if (ThrowOnGet is not null) throw ThrowOnGet;
        return Task.FromResult<IReadOnlyDictionary<string, decimal>>(new Dictionary<string, decimal>(Prices, StringComparer.OrdinalIgnoreCase));
    }
}
