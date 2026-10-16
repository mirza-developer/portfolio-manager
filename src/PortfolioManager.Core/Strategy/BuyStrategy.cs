using PortfolioManager.Core.Domain;

namespace PortfolioManager.Core.Strategy;

public sealed record BuyCandidate(Asset Asset, decimal PriceUsdt, decimal CurrentValueUsdt);

public sealed record PlannedBuy(Asset Asset, decimal SpendUsdt);

public sealed record BuyContext(
    Portfolio Portfolio,
    decimal InvestableUsdt,
    decimal PortfolioValueUsdt,
    IReadOnlyList<BuyCandidate> Candidates);

/// <summary>Plug-in point for the decision-making AI. It only PROPOSES buys; <see cref="TradeRuleGuard"/> always has the last word.</summary>
public interface IBuyStrategy
{
    IReadOnlyList<PlannedBuy> Plan(BuyContext context);
}

/// <summary>Default strategy: split the investable USDT equally across all eligible assets.</summary>
public sealed class EqualWeightBuyStrategy : IBuyStrategy
{
    public IReadOnlyList<PlannedBuy> Plan(BuyContext context)
    {
        if (context.InvestableUsdt <= 0m || context.Candidates.Count == 0)
            return Array.Empty<PlannedBuy>();

        var share = Math.Round(context.InvestableUsdt / context.Candidates.Count, 8, MidpointRounding.ToZero);
        return context.Candidates.Select(c => new PlannedBuy(c.Asset, share)).ToList();
    }
}
