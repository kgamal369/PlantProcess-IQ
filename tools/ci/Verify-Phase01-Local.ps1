# Phase-1 local preflight. Run from the repo root.
# Validates the compose and runs the gate validators if docker/node are present.
$ErrorActionPreference = 'Continue'
$root = (Get-Location).Path

Write-Host '== docker compose config ==' -ForegroundColor Cyan
if (Get-Command docker -ErrorAction SilentlyContinue) {
    Push-Location (Join-Path $root 'deploy/compose')
    docker compose -f docker-compose.yml config | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host '  compose config: OK' -ForegroundColor Green } else { Write-Host '  compose config: FAILED (set deploy/compose/.env)' -ForegroundColor Red }
    Pop-Location
} else { Write-Host '  docker not found - skipping compose validation' -ForegroundColor Yellow }

Write-Host '== gate validators ==' -ForegroundColor Cyan
if (Get-Command node -ErrorAction SilentlyContinue) {
    $vs = @(
        'tools/validation/validate-phase01-phase02-gates.mjs',
        'tools/validation/validate-phase01-phase02-v5-gates.mjs',
        'tools/validation/validate-v7-phase01.cjs',
        'tools/validation/validate-v7-phase01-acceptance.cjs',
        'tools/ci/check-crlf.mjs'
    )
    foreach ($v in $vs) {
        if (-not (Test-Path $v)) { continue }
        node $v
        if ($LASTEXITCODE -eq 0) { Write-Host ("  PASS " + $v) -ForegroundColor Green } else { Write-Host ("  FAIL " + $v) -ForegroundColor Red }
    }
} else { Write-Host '  node not found - skipping validators' -ForegroundColor Yellow }