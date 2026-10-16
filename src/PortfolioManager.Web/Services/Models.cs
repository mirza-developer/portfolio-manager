using PortfolioManager.Core.Domain;

namespace PortfolioManager.Web.Services;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record PortfolioSummary(
    int Id, string Name, string OwnerName, decimal UsdtBalance, decimal TotalValueUsdt,
    decimal StopLossPercent, bool IsAutoTradingEnabled, int HoldingCount);

public sealed record PositionRow(
    int Id, int AssetId, string Symbol, decimal Quantity, decimal ValueUsdt, decimal BenchmarkValueUsdt,
    decimal InitialValueUsdt, decimal DrawdownPercent, decimal LimitPercent, decimal? StopLossPercentOverride,
    PositionStatus Status, bool HasPrice);

public sealed class PortfolioSettings
{
    public decimal StopLossPercent { get; set; } = 5m;
    public decimal MaxAllocationPercent { get; set; } = 40m;
    public decimal CashReservePercent { get; set; } = 10m;
    public int CooldownHours { get; set; } = 4;
    public decimal ReentryRecoveryPercent { get; set; } = 1m;
    public bool IsAutoTradingEnabled { get; set; } = true;
}

public sealed record PortfolioDetails(
    int Id, string Name, string OwnerName, decimal UsdtBalance, decimal TotalValueUsdt,
    PortfolioSettings Settings, IReadOnlyList<PositionRow> Positions);

public sealed class PositionEdit
{
    public decimal Quantity { get; set; }
    public decimal InitialValueUsdt { get; set; }
    public decimal? StopLossPercentOverride { get; set; }
}

public sealed record TradeLogRow(
    long Id, DateTime TimestampUtc, string PortfolioName, string Symbol, TradeSide Side, TradeReason Reason,
    decimal Quantity, decimal PriceUsdt, decimal ValueUsdt, decimal FeeUsdt);

public sealed record PriceLogRow(long Id, DateTime TimestampUtc, string Symbol, decimal PriceUsdt, string Source);

/// <summary>ToUtc is EXCLUSIVE (the page passes the day after the picked end date).</summary>
public sealed class TradeLogFilter
{
    public int? PortfolioId { get; set; }
    public int? AssetId { get; set; }
    public TradeSide? Side { get; set; }
    public TradeReason? Reason { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class PriceLogFilter
{
    public int? AssetId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class AssetEdit
{
    public int? Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public decimal MinTradeSizeUsdt { get; set; } = 10m;
    public string PriceSource { get; set; } = "simulated";
}

public sealed record UserRow(string Id, string Email, string DisplayName, string Role, bool IsActive);

public sealed class UserEdit
{
    public string? Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record DashboardData(
    decimal TotalValueUsdt, decimal UsdtBalance, int HoldingCount, DateTime? LastTradeUtc, IReadOnlyList<TradeLogRow> LatestTrades);
