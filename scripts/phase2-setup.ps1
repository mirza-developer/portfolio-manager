# Restores tools and packages, creates the InitialCreate migration for BOTH providers (if missing), builds and runs all tests.
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

dotnet tool restore
dotnet restore
dotnet build --no-restore

foreach ($provider in 'SqlServer', 'Sqlite') {
    $project = "src/PortfolioManager.Data.Migrations.$provider"
    if (-not (Get-ChildItem "$project/Migrations" -Filter '*_InitialCreate.cs' -ErrorAction SilentlyContinue)) {
        dotnet ef migrations add InitialCreate --project $project --startup-project $project --context AppDbContext --output-dir Migrations
        if ($LASTEXITCODE -ne 0) { throw "Creating the $provider migration failed." }
    }
}

dotnet build
dotnet test
