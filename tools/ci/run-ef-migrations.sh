#!/usr/bin/env sh
set -eu

echo "============================================================"
echo "PlantProcess IQ — EF Core Migration Runner"
echo "============================================================"

if [ -z "${ConnectionStrings__PlantProcessDb:-}" ]; then
  echo "[migration] ERROR: ConnectionStrings__PlantProcessDb environment variable is missing."
  exit 2
fi

echo "[migration] Installing dotnet-ef..."
dotnet tool install --global dotnet-ef --version 9.0.4 >/tmp/dotnet-ef-install.log 2>&1 || true

export PATH="$PATH:/root/.dotnet/tools"

echo "[migration] dotnet version:"
dotnet --version

echo "[migration] dotnet ef version:"
dotnet ef --version

echo "[migration] Applying migrations..."
dotnet ef database update \
  --project PlantProcess.Infrastructure \
  --startup-project PlantProcess.Api \
  --context PlantProcessDbContext \
  --configuration Release

echo "============================================================"
echo "PlantProcess IQ — EF Core Migration Completed"
echo "============================================================"