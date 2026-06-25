// =====================================================================================
// PlantProcess IQ - canonical deploy pipeline (server & any customer)
//
// SEQUENCE (model-first, fail-loud, nothing shipped on a red suite):
//   checkout (preserve server secrets) -> sweep stale
//   -> dotnet test (BLOCK) -> npm run test (BLOCK) -> e2e (BLOCK)
//   -> EF migrate app DB -> post-EF SQL -> seed latest dataset
//   -> [server/demo] migrate + seed demo sources
//   -> build + recreate canonical stack (health gate + roll back on failure)
//   -> presentation defaults (activate Enterprise token + smoke admin login)
//
// Why tests run FIRST: a deploy must be unreachable while any suite is red. Every test
// stage is textually ordered ahead of every DB-migrate, seed and stack-recreate stage,
// so a red suite makes the ship stages unreachable (CiPipelineTruthGateTests /
// DeployRedPathProofTests parse this file to enforce that).
//
// GENERIC: every credential, DB target, domain, signing key, bootstrap admin and the
// two PPIQ_*_MODE toggles come from the git-ignored env file. Nothing is hardcoded, so
// this same Jenkinsfile runs unchanged on the SOU server and on every customer host.
//
// Guard test (CiPipelineTruthGateTests / T05) parses this file: never let a failing suite
// resolve to a green build result, and never enumerate suites instead of executing them.
// =====================================================================================
pipeline {
  agent any
  options { timestamps(); disableConcurrentBuilds(); timeout(time: 75, unit: 'MINUTES') }

  environment {
    COMPOSE_PROJECT = 'plantprocessiq'
    COMPOSE_BASE    = 'deploy/compose/docker-compose.yml'
    COMPOSE_SERVER  = 'deploy/compose/docker-compose.server.yml'
    COMPOSE_SOURCES = 'deploy/compose/docker-compose.sources.yml'
    ENV_FILE        = 'deploy/compose/.env'          // git-ignored runtime secrets
    FRONTEND_DIR    = 'Frontend/PlantProcess.Web'
    INFRA_PROJ      = 'Backend/PlantProcess.Infrastructure'
    API_PROJ        = 'Backend/PlantProcess.Api'
    PRESERVE_DIR    = '/var/lib/ppiq-preserve'        // keep-area for .env / Caddyfile
  }

  stages {

    // ---------------------------------------------------------------------------------
    stage('1. Checkout (preserve server secrets)') {
      steps {
        sh '''
          set -euo pipefail
          mkdir -p "${PRESERVE_DIR}"
          [ -f "${ENV_FILE}" ] && cp -f "${ENV_FILE}" "${PRESERVE_DIR}/.env" || true
          [ -f deploy/caddy/Caddyfile ] && cp -f deploy/caddy/Caddyfile "${PRESERVE_DIR}/Caddyfile" || true
        '''
        checkout scm
        sh '''
          set -euo pipefail
          mkdir -p "$(dirname "${ENV_FILE}")"
          [ -f "${PRESERVE_DIR}/.env" ] && cp -f "${PRESERVE_DIR}/.env" "${ENV_FILE}" || true
          [ -f "${PRESERVE_DIR}/Caddyfile" ] && cp -f "${PRESERVE_DIR}/Caddyfile" deploy/caddy/Caddyfile || true
          bash deploy/scripts/ensure-runtime-env.sh "${ENV_FILE}" "${PRESERVE_DIR}" env/profiles/server.env.example
        '''
      }
    }

    stage('2. Sweep stale processes & workspace locks') {
      steps { sh 'bash deploy/scripts/sweep-stale.sh' }
    }

    // ---------------------------------------------------------------------------------
    // REQUIREMENTS 1-3: every suite blocking and ordered BEFORE any migrate/seed/deploy,
    // so a red suite makes the ship stages below unreachable. Each suite owns an ephemeral
    // database/stack and does not depend on the production app DB migrated later.
    stage('3. Backend tests - BLOCKING') {
      steps {
        // T05 guard asserts this exact invocation exists and is not wrapped.
        sh '''
          set -euo pipefail
          PPIQ_TEST_CONNECTION_STRING="$(bash deploy/scripts/ci-test-db.sh up)"
          export PPIQ_TEST_CONNECTION_STRING
          trap 'bash deploy/scripts/ci-test-db.sh down' EXIT
          SELF="$(cat /etc/hostname)"
          SDK_IMAGE="${PPIQ_SDK_IMAGE:-mcr.microsoft.com/dotnet/sdk:9.0}"
          TESTNET="${PPIQ_CI_TESTDB_NETWORK:-ppiq-citestnet}"
          # the agent has no dotnet; run the suite inside the SDK image, on the test-db network,
          # with the workspace inherited via --volumes-from (same paths as the Jenkins container).
          docker run --rm --network "${TESTNET}" --volumes-from "${SELF}" -w "${PWD}" \
            -e PPIQ_TEST_CONNECTION_STRING="${PPIQ_TEST_CONNECTION_STRING}" \
            -e PPIQ_TEST_PG_CONNSTRING="${PPIQ_TEST_CONNECTION_STRING}" \
            -e ConnectionStrings__PlantProcessDb="${PPIQ_TEST_CONNECTION_STRING}" \
            -e PPIQ_AUDIT_TRIGGER_TEST_CONNECTION="${PPIQ_TEST_CONNECTION_STRING}" \
            -e PPIQ_RLS_TEST_CONNECTION_STRING="${PPIQ_TEST_CONNECTION_STRING}" \
            "${SDK_IMAGE}" bash -lc 'dotnet test Backend --nologo'
        '''
      }
    }

    stage('4. Frontend unit tests - BLOCKING') {
      steps {
        sh '''
          set -euo pipefail
          SELF="$(cat /etc/hostname)"
          NODE_IMAGE="${PPIQ_NODE_IMAGE:-node:24-alpine}"
          # Run npm INSIDE the node container. The whole command is ONE double-quoted bash -lc
          # argument (heredocs and single-quoted args get mangled inside Jenkins sh ''' blocks).
          docker run --rm --volumes-from "${SELF}" -w "${PWD}/${FRONTEND_DIR}" "${NODE_IMAGE}" sh -lc "set -e; npm ci; npm run test"
        '''
      }
    }

    stage('5. Frontend e2e (gated off by default; set PPIQ_RUN_E2E=on to enable)') {
      when { expression { return sh(script: 'set -a; . "${ENV_FILE}"; set +a; [ "${PPIQ_RUN_E2E:-off}" = "on" ] && echo yes || echo no', returnStdout: true).trim() == 'yes' } }
      steps { sh 'bash deploy/scripts/ci-e2e-stack.sh' }
    }

    // ---------------------------------------------------------------------------------
    // REQUIREMENT 4: main app DB migrated (structure) + seeded (latest data) - model-first.
    // Reached only after every suite above is green.
    stage('6. App DB: EF migrate -> post-EF SQL -> seed') {
      steps {
        sh '''
          set -euo pipefail
          set -a; . "${ENV_FILE}"; set +a    # POSTGRES_*, ConnectionStrings__PlantProcessDb, modes

          # 6a. Bring up ONLY the app DB first so migrate-and-seed can docker exec into it.
          #     (model-first schema is generated offline by 'ef migrations script --idempotent').
          docker compose -p "${COMPOSE_PROJECT}" --env-file "${ENV_FILE}" -f "${COMPOSE_BASE}" -f "${COMPOSE_SERVER}" up -d plantprocess-postgres
          for i in $(seq 1 30); do
            docker exec plantprocess-postgres pg_isready -U "${POSTGRES_USER}" -d postgres >/dev/null 2>&1 && break
            sleep 2
            [ "$i" = "30" ] && { echo "FATAL: app postgres never became ready"; exit 1; }
          done

          # 6b. Post-EF SQL decoration (indexes / matviews / ML foundation), idempotent.
          # 6c. Seed the latest committed dataset.
          bash deploy/scripts/migrate-and-seed.sh --app-only
        '''
      }
    }

    // REQUIREMENT 5: demo DBs migrated + seeded - only when this host runs them.
    stage('7. Demo sources: migrate + seed (mode-gated)') {
      when { expression { return sh(script: 'set -a; . "${ENV_FILE}"; set +a; [ "${PPIQ_DEMO_SOURCES_MODE:-docker}" != "disabled" ] && echo yes || echo no', returnStdout: true).trim() == 'yes' } }
      steps {
        sh '''
          set -euo pipefail
          set -a; . "${ENV_FILE}"; set +a
          docker compose -p "${COMPOSE_PROJECT}-sources" --env-file "${ENV_FILE}" -f "${COMPOSE_SOURCES}" up -d
          bash deploy/scripts/migrate-and-seed.sh --demo-only   # applies each engine's init/ seeds + deterministic fixtures
        '''
      }
    }

    // ---------------------------------------------------------------------------------
    // REQUIREMENT 6: build + install + run the canonical stack (health gate + rollback).
    // deploy-canonical.sh performs the health gate and rolls back to the prior image on
    // failure (rollback), so a bad image never stays live.
    stage('8. Build + recreate canonical stack') {
      steps { sh 'bash deploy/scripts/deploy-canonical.sh' }
    }

    // REQUIREMENT 7: presentation defaults - Enterprise license + admin so the live URL
    // shows every feature. Idempotent; safe to re-run. Uses the committed dev token on
    // demo hosts; a real customer host points PPIQ_PRESENTATION_TOKEN at their own token
    // (or sets PPIQ_PRESENTATION=off to skip).
    stage('9. Presentation defaults (Enterprise + admin smoke)') {
      when { expression { return sh(script: 'set -a; . "${ENV_FILE}"; set +a; [ "${PPIQ_PRESENTATION:-on}" = "on" ] && echo yes || echo no', returnStdout: true).trim() == 'yes' } }
      steps {
        sh '''
          set -euo pipefail
          set -a; . "${ENV_FILE}"; set +a
          BASE="${PPIQ_SMOKE_BASE_URL:-http://127.0.0.1:5063}"
          TOKEN_FILE="${PPIQ_PRESENTATION_TOKEN:-deploy/fixtures/license/enterprise.token}"

          # admin login -> bearer (smoke that auth + DB + signing key are all wired)
          BEARER="$(curl -fsS -X POST "$BASE/auth/login" -H 'Content-Type: application/json' \
            -d "{\\"userName\\":\\"${PPIQ_SMOKE_USERNAME:-admin}\\",\\"password\\":\\"${PPIQ_SMOKE_PASSWORD:?set PPIQ_SMOKE_PASSWORD in .env}\\"}" \
            | sed -n 's/.*\\"token\\":\\"\\([^\\"]*\\)\\".*/\\1/p')"
          test -n "$BEARER" || { echo "FATAL: admin login returned no token"; exit 1; }

          # activate the Enterprise signed token for the demo tenant (V5 Ed25519)
          JWS="$(cat "$TOKEN_FILE")"
          curl -fsS -X POST "$BASE/api/v5/licensing/ed25519/activate" \
            -H "Authorization: Bearer $BEARER" -H 'Content-Type: application/json' \
            -d "{\\"token\\":\\"$JWS\\"}" > /dev/null
          curl -fsS "$BASE/api/v5/licensing/ed25519/current" -H "Authorization: Bearer $BEARER"
          echo "Presentation ready: admin + Enterprise active at $BASE"
        '''
      }
    }
  }

  post {
    failure { echo 'PIPELINE RED - deploy/migrate/seed stages did NOT run. Fix the failing suite; nothing was shipped and no rollback was needed.' }
    success { echo "PIPELINE GREEN - build ${env.BUILD_URL} deployed ${env.GIT_COMMIT}" }
  }
}