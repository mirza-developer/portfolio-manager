namespace PortfolioManager.Core.Strategy;

/// <summary>Hard rules applied to every proposed buy, whatever strategy produced it.</summary>
public sealed class TradeRuleGuard
{
    public IReadOnlyList<PlannedBuy> Apply(BuyContext context, IReadOnlyList<PlannedBuy> planned)
    {
        var capped = new List<PlannedBuy>();
        foreach (var plan in planned.GroupBy(p => p.Asset.Id).Select(g => g.First()))
        {
            var candidate = context.Candidates.FirstOrDefault(c => c.Asset.Id == plan.Asset.Id);
            if (candidate is null || plan.SpendUsdt <= 0m) continue;

            // Max allocation: the asset value after the buy may not exceed X% of the portfolio value.
            var cap = Math.Max(0m, context.Portfolio.MaxAllocationPercent / 100m * context.PortfolioValueUsdt - candidate.CurrentValueUsdt);
            var spend = Math.Min(plan.SpendUsdt, cap);
            if (spend > 0m) capped.Add(new PlannedBuy(plan.Asset, spend));
        }

        // Never spend more than the investable amount (this also protects the cash reserve).
        var total = capped.Sum(p => p.SpendUsdt);
        if (total > context.InvestableUsdt && total > 0m)
        {
            var factor = context.InvestableUsdt / total;
            capped = capped
                .Select(p => new PlannedBuy(p.Asset, Math.Round(p.SpendUsdt * factor, 8, MidpointRounding.ToZero)))
                .ToList();
        }

        return capped.Where(p => p.SpendUsdt > 0m && p.SpendUsdt >= p.Asset.MinTradeSizeUsdt).ToList();
    }
}
