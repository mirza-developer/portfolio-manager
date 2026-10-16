using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PortfolioManager.Core;
using PortfolioManager.Data;
using PortfolioManager.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Runs as a Windows Service when installed as one; logs go to the Windows Event Log as well as the console.
builder.Services.AddWindowsService(options => options.ServiceName = "PortfolioManagerTrading");

builder.Services.AddDataLayer(builder.Configuration);
builder.Services.AddTradingCore();
builder.Services.Configure<TradingOptions>(builder.Configuration.GetSection("Trading"));

var priceProvider = builder.Configuration["Trading:PriceProvider"] ?? "Simulated";
if (!string.Equals(priceProvider, "Simulated", StringComparison.OrdinalIgnoreCase))
    throw new NotSupportedException($"Trading:PriceProvider '{priceProvider}' is not supported yet. Use 'Simulated'.");
builder.Services.AddSimulatedExchange();

builder.Services.AddHostedService<TradingWorker>();

await builder.Build().RunAsync();
