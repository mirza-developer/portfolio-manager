using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PortfolioManager.Data.Tests;

public static class ModelDiff
{
    /// <summary>True when the current model differs from the migrations snapshot (a migration is missing).</summary>
    public static bool HasPendingChanges(DbContext context)
    {
        var differ = context.GetService<IMigrationsModelDiffer>();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var designTimeModel = context.GetService<IDesignTimeModel>();

        var snapshotModel = migrationsAssembly.ModelSnapshot?.Model;
        if (snapshotModel is IMutableModel mutable) snapshotModel = mutable.FinalizeModel();
        if (snapshotModel is not null) snapshotModel = initializer.Initialize(snapshotModel);

        return differ.HasDifferences(snapshotModel?.GetRelationalModel(), designTimeModel.Model.GetRelationalModel());
    }
}
