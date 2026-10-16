#!/usr/bin/env bash
# Restores tools and packages, creates the InitialCreate migration for BOTH providers (if missing), builds and runs all tests.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet tool restore
dotnet restore
dotnet build --no-restore

for provider in SqlServer Sqlite; do
  project="src/PortfolioManager.Data.Migrations.$provider"
  if ! ls "$project"/Migrations/*_InitialCreate.cs >/dev/null 2>&1; then
    dotnet ef migrations add InitialCreate --project "$project" --startup-project "$project" --context AppDbContext --output-dir Migrations
  fi
done

dotnet build
dotnet test
