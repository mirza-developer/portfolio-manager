using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortfolioManager.Core;
using PortfolioManager.Core.Engine;

namespace PortfolioManager.Worker;

/// <summary>
/// Host loop only: runs one trading cycle immediately, then one per <see cref="TradingOptions.CycleInterval"/>.
/// It contains NO trading logic; every cycle is delegated to <see cref="ITradingEngine"/> in a fresh DI scope.
/// A failing cycle is logged and never stops the loop.
/// </summary>
public sealed class TradingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<TradingOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<TradingWorker> _logger;

    public TradingWorker(IServiceScopeFactory scopes, IOptions<TradingOptions> options, TimeProvider time, ILogger<TradingWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = _options.Value.CycleInterval;
        if (interval <= TimeSpan.Zero)
            throw new InvalidOperationException("Trading:CycleInterval must be greater than zero.");

        _logger.LogInformation("Trading worker started. A cycle runs every {Interval}.", interval);

        await RunCycleSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval, _time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunCycleSafelyAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }

        _logger.LogInformation("Trading worker stopped.");
    }

    private async Task RunCycleSafelyAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<ITradingEngine>();
            var result = await engine.RunCycleAsync(ct);
            _logger.LogInformation(
                "Cycle finished: {Processed} portfolio(s) processed, {Failed} failed, {Trades} trade(s).",
                result.PortfoliosProcessed, result.PortfoliosFailed, result.TradesExecuted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trading cycle failed; the next cycle will run as scheduled.");
        }
    }
}
