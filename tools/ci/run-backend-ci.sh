#!/usr/bin/env sh
set -eu

echo "============================================================"
echo "PlantProcess IQ — Backend CI"
echo "============================================================"

cd Backend

echo "[backend-ci] Restore"
dotnet restore PlantProcessIQ.sln

echo "[backend-ci] Build"
dotnet build PlantProcessIQ.sln -c Release --no-restore

echo "[backend-ci] Test with coverage"
dotnet test PlantProcessIQ.sln \
  -c Release \
  --no-build \
  --settings coverlet.runsettings \
  --collect:"XPlat Code Coverage" \
  --logger "trx;LogFileName=backend-tests.trx" \
  --results-directory ./TestResults

echo "============================================================"
echo "PlantProcess IQ — Backend CI PASSED"
echo "============================================================"