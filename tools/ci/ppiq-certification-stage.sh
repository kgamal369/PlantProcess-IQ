#!/usr/bin/env sh
set -eu

PROJECT_ROOT="${PROJECT_ROOT:-$(pwd)}"
cd "$PROJECT_ROOT"

# PPIQ_PACK_A3_CI_CERTIFICATION
# taskClosure: T001-T071 task closure gate
node tools/task-closure/validate-t001-t071-task-closure.cjs
node tools/task-closure/ppiq-pack-a-scorecard-bridge.cjs

# routeContract: Pack D route contract snapshot
node tools/pack-d/validate-pack-d-route-contract-snapshot.cjs

node tools/pack-b/validate-pack-b-p05-closure.cjs
node tools/pack-d/validate-pack-d-backend-thinness.cjs
node tools/phase56/validate-phase56.cjs
node tools/ci/write-certification-gate-report.cjs

echo "PPIQ certification gates completed."
