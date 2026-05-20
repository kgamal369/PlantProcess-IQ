#!/usr/bin/env sh
set -eu

echo "============================================================"
echo "PlantProcess IQ — Frontend App CI"
echo "============================================================"

cd Frontend/PlantProcess.Web

echo "[frontend-ci] npm ci"
npm ci

echo "[frontend-ci] TypeScript build + Vite build"
npm run build

echo "[frontend-ci] Lint"
npm run lint

echo "[frontend-ci] Unit tests"
npm run test

echo "[frontend-ci] Language audit"
npm run language:audit

echo "============================================================"
echo "PlantProcess IQ — Frontend App CI PASSED"
echo "============================================================"