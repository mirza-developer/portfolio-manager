using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data.Identity;

namespace PortfolioManager.Data.Seeding;

/// <summary>Idempotent: roles, the admin user (when configured), the GOLD and SILVER assets and an optional demo portfolio.</summary>
public sealed class DbSeeder(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles,
    IOptions<SeedOptions> options,
    TimeProvider time)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var settings = options.Value;

        foreach (var role in new[] { AppRoles.Admin, AppRoles.User })
        {
            if (!await roles.RoleExistsAsync(role))
                Ensure(await roles.CreateAsync(new ApplicationRole(role)), $"create role {role}");
        }

        ApplicationUser? admin = null;
        if (!string.IsNullOrWhiteSpace(settings.AdminEmail) && !string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            admin = await users.FindByEmailAsync(settings.AdminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = settings.AdminEmail,
                    Email = settings.AdminEmail,
                    EmailConfirmed = true,
                    DisplayName = "Administrator",
                    IsActive = true
                };
                Ensure(await users.CreateAsync(admin, settings.AdminPassword), "create the admin user");
            }
            if (!await users.IsInRoleAsync(admin, AppRoles.Admin))
                Ensure(await users.AddToRoleAsync(admin, AppRoles.Admin), "add the admin user to the Admin role");
        }

        foreach (var (symbol, name) in new[] { ("GOLD", "Digital Gold"), ("SILVER", "Digital Silver") })
        {
            if (!await db.Assets.AnyAsync(a => a.Symbol == symbol, ct))
                db.Assets.Add(new Asset { Symbol = symbol, Name = name, IsEnabled = true, MinTradeSizeUsdt = 10m, PriceSource = "simulated" });
        }
        await db.SaveChangesAsync(ct);

        if (settings.CreateDemoPortfolio && admin is not null && !await db.Portfolios.AnyAsync(p => p.UserId == admin.Id, ct))
        {
            var now = time.GetUtcNow().UtcDateTime;
            var gold = await db.Assets.SingleAsync(a => a.Symbol == "GOLD", ct);
            var silver = await db.Assets.SingleAsync(a => a.Symbol == "SILVER", ct);
            db.Portfolios.Add(new Portfolio
            {
                UserId = admin.Id,
                Name = "Demo portfolio",
                UsdtBalance = 1000m,
                CreatedUtc = now,
                Positions =
                {
                    NewHolding(gold.Id, 5m, 500m, now),
                    NewHolding(silver.Id, 5m, 500m, now)
                }
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static Position NewHolding(int assetId, decimal quantity, decimal initialValue, DateTime now) => new()
    {
        AssetId = assetId,
        Quantity = quantity,
        InitialValueUsdt = initialValue,
        BenchmarkValueUsdt = initialValue,
        Status = PositionStatus.Holding,
        UpdatedUtc = now
    };

    private static void Ensure(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Seeding failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
