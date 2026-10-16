using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortfolioManager.Core.Domain;
using PortfolioManager.Data.Identity;

namespace PortfolioManager.Data.Configurations;

// Decimal precision (28,10) and the UTC DateTime converters are set once, in AppDbContext.ConfigureConventions.

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Symbol).IsRequired().HasMaxLength(20);
        b.Property(a => a.Name).IsRequired().HasMaxLength(100);
        b.Property(a => a.PriceSource).IsRequired().HasMaxLength(50);
        b.HasIndex(a => a.Symbol).IsUnique();
    }
}

public sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(p => p.UserId);

        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Positions).WithOne().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.HasKey(p => p.Id);
        b.HasIndex(p => new { p.PortfolioId, p.AssetId }).IsUnique();
        b.HasOne(p => p.Asset).WithMany().HasForeignKey(p => p.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TradeLogConfiguration : IEntityTypeConfiguration<TradeLog>
{
    public void Configure(EntityTypeBuilder<TradeLog> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Note).HasMaxLength(500);
        b.HasIndex(l => new { l.PortfolioId, l.TimestampUtc });
        b.HasOne<Portfolio>().WithMany().HasForeignKey(l => l.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(l => l.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PriceLogConfiguration : IEntityTypeConfiguration<PriceLog>
{
    public void Configure(EntityTypeBuilder<PriceLog> b)
    {
        b.HasKey(l => l.Id);
        b.Property(l => l.Source).IsRequired().HasMaxLength(50);
        b.HasIndex(l => new { l.AssetId, l.TimestampUtc });
        b.HasOne<Asset>().WithMany().HasForeignKey(l => l.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
