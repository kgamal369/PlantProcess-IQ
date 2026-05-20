#!/usr/bin/env sh
set -eu

echo "============================================================"
echo "PlantProcess IQ — Website CI"
echo "============================================================"

cd Website/PlantProcess.Website

echo "[website-ci] npm ci"
npm ci

echo "[website-ci] Build"
npm run build

echo "============================================================"
echo "PlantProcess IQ — Website CI PASSED"
echo "============================================================"