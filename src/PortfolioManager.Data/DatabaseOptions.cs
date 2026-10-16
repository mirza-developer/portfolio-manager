namespace PortfolioManager.Data;

public enum DatabaseProvider
{
    SqlServer = 0,
    Sqlite = 1
}

public sealed class DatabaseOptions
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Sqlite;
    public string ConnectionString { get; set; } = "Data Source=portfolio.db";
    /// <summary>When true, <see cref="DataServiceCollectionExtensions.InitializeDatabaseAsync"/> applies pending migrations. Use in the Web project only.</summary>
    public bool AutoMigrate { get; set; }
}
