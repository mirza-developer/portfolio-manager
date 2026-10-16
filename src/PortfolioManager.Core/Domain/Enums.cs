namespace PortfolioManager.Core.Domain;

public enum PositionStatus { None = 0, Holding = 1, Cooldown = 2 }

public enum TradeSide { Buy = 1, Sell = 2 }

public enum TradeReason { Initial = 1, StopLoss = 2, Reinvest = 3, Manual = 4 }
