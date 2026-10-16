# Usage: scripts/add-migration.ps1 <MigrationName>   (creates the SAME migration in BOTH providers)
param([Parameter(Mandatory = $true)][string]$Name)
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet tool restore
foreach ($provider in 'SqlServer', 'Sqlite') {
    $project = "src/PortfolioManager.Data.Migrations.$provider"
    dotnet ef migrations add $Name --project $project --startup-project $project --context AppDbContext --output-dir Migrations
    if ($LASTEXITCODE -ne 0) { throw "Creating the $provider migration failed." }
}
