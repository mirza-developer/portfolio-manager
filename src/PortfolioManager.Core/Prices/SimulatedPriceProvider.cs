using PortfolioManager.Core.Abstractions;
using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Prices;

/// <summary>
/// Deterministic random-walk prices for tests and demos. The first call returns the start price of each symbol,
/// every later call moves each price by a random step of at most <c>volatilityPercent</c>. Prices are always positive.
/// </summary>
public sealed class SimulatedPriceProvider : IPriceProvider
{
    private const decimal MinPrice = 0.00000001m;

    private readonly object _gate = new();
    private readonly Random _random;
    private readonly decimal _volatilityPercent;
    private readonly decimal _defaultStartPrice;
    private readonly IReadOnlyDictionary<string, decimal> _startPrices;
    private readonly Dictionary<string, decimal> _current = new(StringComparer.OrdinalIgnoreCase);

    public SimulatedPriceProvider(
        int seed = 42,
        decimal volatilityPercent = 1m,
        IReadOnlyDictionary<string, decimal>? startPrices = null,
        decimal defaultStartPrice = 100m)
    {
        _random = new Random(seed);
        _volatilityPercent = volatilityPercent;
        _defaultStartPrice = defaultStartPrice;
        _startPrices = startPrices ?? new Dictionary<string, decimal>();
    }

    public Task<IReadOnlyDictionary<string, decimal>> GetPricesAsync(IReadOnlyCollection<Asset> assets, CancellationToken ct = default)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            foreach (var asset in assets.OrderBy(a => a.Id))
            {
                if (_current.TryGetValue(asset.Symbol, out var last))
                {
                    var step = (decimal)(_random.NextDouble() * 2d - 1d) * _volatilityPercent / 100m;
                    _current[asset.Symbol] = Math.Max(MinPrice, Math.Round(last * (1m + step), 8));
                }
                else
                {
                    var start = _startPrices.TryGetValue(asset.Symbol, out var configured) ? configured : _defaultStartPrice;
                    _current[asset.Symbol] = Math.Max(MinPrice, start);
                }
                result[asset.Symbol] = _current[asset.Symbol];
            }
        }
        return Task.FromResult<IReadOnlyDictionary<string, decimal>>(result);
    }
}
