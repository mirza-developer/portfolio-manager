#!/usr/bin/env bash
# Usage: scripts/add-migration.sh <MigrationName>   (creates the SAME migration in BOTH providers)
set -euo pipefail
cd "$(dirname "$0")/.."
name="${1:?Usage: scripts/add-migration.sh <MigrationName>}"
dotnet tool restore
for provider in SqlServer Sqlite; do
  project="src/PortfolioManager.Data.Migrations.$provider"
  dotnet ef migrations add "$name" --project "$project" --startup-project "$project" --context AppDbContext --output-dir Migrations
done
