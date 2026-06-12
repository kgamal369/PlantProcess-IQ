// =====================================================================================
// PPIQ canonical pipeline (T02, 12 Jun 2026)
// SEQUENCE (agreed go-live definition - NOTHING else):
//   checkout -> sweep stale -> dotnet test (BLOCK) -> npm test (BLOCK) -> npm e2e (BLOCK)
//   -> migrate app + demo DBs -> seed demo data -> build + recreate canonical stack
// Guard test (T05) parses this file: do NOT add catchError(SUCCESS) or --list anywhere.
// =====================================================================================
pipeline {
  agent any
  options { timestamps(); disableConcurrentBuilds(); timeout(time: 60, unit: 'MINUTES') }

  environment {
    COMPOSE_PROJECT = 'plantprocessiq'
    COMPOSE_FILE    = 'Infrastructure/deploy/docker-compose.demo.yml'
    ENV_FILE        = 'deploy/compose/.env'
    FRONTEND_DIR    = 'Frontend/PlantProcess.Web'
    PRESERVE_DIR    = '/var/lib/ppiq-preserve'    // server-side keep-area for .env / Caddyfile
  }

  stages {

    stage('1. Checkout (preserve server config)') {
      steps {
        sh '''
          set -euo pipefail
          mkdir -p "${PRESERVE_DIR}"
          [ -f "${ENV_FILE}" ] && cp -f "${ENV_FILE}" "${PRESERVE_DIR}/.env" || true
          [ -f Infrastructure/deploy/Caddyfile ] && cp -f Infrastructure/deploy/Caddyfile "${PRESERVE_DIR}/Caddyfile" || true
        '''
        checkout scm
        sh '''
          set -euo pipefail
          [ -f "${PRESERVE_DIR}/.env" ] && mkdir -p "$(dirname "${ENV_FILE}")" && cp -f "${PRESERVE_DIR}/.env" "${ENV_FILE}" || true
          [ -f "${PRESERVE_DIR}/Caddyfile" ] && cp -f "${PRESERVE_DIR}/Caddyfile" Infrastructure/deploy/Caddyfile || true
        '''
      }
    }

    stage('2. Sweep stale processes & workspace locks') {
      steps { sh 'bash deploy/scripts/sweep-stale.sh' }
    }

    stage('3. Backend tests - BLOCKING') {
      steps {
        // T05 guard asserts this exact invocation exists and is not wrapped.
        sh 'dotnet test Backend --nologo'
      }
    }

    stage('4. Frontend unit tests - BLOCKING') {
      steps {
        dir("${FRONTEND_DIR}") {
          sh '''
            set -euo pipefail
            npm ci
            npm run test
          '''
        }
      }
    }

    stage('5. Frontend e2e - BLOCKING (ephemeral CI stack)') {
      steps { sh 'bash deploy/scripts/ci-e2e-stack.sh' }
    }

    stage('6. Migrate app + demo DBs and seed') {
      steps { sh 'bash deploy/scripts/migrate-and-seed.sh' }
    }

    stage('7. Build + recreate canonical stack (health gate + rollback)') {
      steps { sh 'bash deploy/scripts/deploy-canonical.sh' }
    }
  }

  post {
    failure { echo 'PIPELINE RED - deploy stages did NOT run. Fix the failing suite; nothing was shipped.' }
    success { echo "PIPELINE GREEN - build ${env.BUILD_URL} deployed ${env.GIT_COMMIT}" }
  }
}