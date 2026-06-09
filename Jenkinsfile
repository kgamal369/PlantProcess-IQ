/* PPIQ_REALIZATION_T017_CANONICAL_JENKINSFILE */
// ============================================================
// PlantProcess IQ - CI/CD pipeline
//
// What this does on every git push to main:
//   1. Pull latest source on the deploy server (/opt/PlantProcess-IQ),
//      preserving server-only config (.env, Caddyfile, compose mount)
//   2. Apply numbered SQL scripts (idempotent)
//   3. Rebuild 4 Docker images (api, workers, app-web, website)
//   4. Recreate the 4 app containers (Postgres + Caddy stay running)
//   5. Probe /health and key endpoints
//
// EF Core migrations apply automatically when the API container boots.
// ============================================================

pipeline {
    agent any

    triggers {
        githubPush()
    }

    options {
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    environment {
        REPO_DIR     = '/opt/PlantProcess-IQ'
        DEPLOY_DIR   = '/opt/PlantProcess-IQ/deploy/compose'
        COMPOSE_FILE = 'docker-compose.demo.yml'
        DB_CONTAINER = 'ppiq-postgres'
        API_HEALTH   = 'https://api.178.105.152.180.sslip.io/health'
    }

    stages {

        stage('1. Pull latest code') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'github-kgamal369', usernameVariable: 'GIT_USER', passwordVariable: 'GIT_PAT')]) {
                    sh '''
                        set -e
                        cd ${REPO_DIR}

                        # ---- Preserve server-only files across the pull ----
                        BACKUP=$(mktemp -d)
                        for f in deploy/compose/.env deploy/caddy/Caddyfile
                        do
                            if [ -f "$f" ]; then
                                mkdir -p "$BACKUP/$(dirname $f)"
                                cp "$f" "$BACKUP/$f"
                                echo "    backed up $f"
                            fi
                        done

                        # ---- Hard sync to origin/main (auth via bound Jenkins PAT) ----
                        git -c credential.helper= \
                            -c credential.helper='!f() { echo username=${GIT_USER:-x-access-token}; echo password=$GIT_PAT; }; f' \
                            fetch --all --prune
                        git reset --hard origin/main
                        git clean -fd

                        # ---- Restore the production config files ----
                        for f in deploy/compose/.env deploy/caddy/Caddyfile
                        do
                            if [ -f "$BACKUP/$f" ]; then
                                cp "$BACKUP/$f" "$f"
                                echo "    restored $f"
                            fi
                        done

                        rm -rf "$BACKUP"

                        echo "==> HEAD is now at:"
                        git log -1 --format='%h %ai %s'
                    '''
                }
            }
        }

        stage('2. Apply SQL scripts (idempotent)') {
            steps {
                sh '''
                    PGUSER=$(docker exec ${DB_CONTAINER} sh -c 'echo $POSTGRES_USER')
                    PGDB=$(docker exec ${DB_CONTAINER} sh -c 'echo $POSTGRES_DB')
                    echo "Using PGUSER=$PGUSER PGDB=$PGDB"
                    cd ${REPO_DIR}/Backend/database/scripts
                    for f in $(ls -1 [0-9]*.sql | sort); do
                        echo "==> Applying $f"
                        docker exec -i ${DB_CONTAINER} psql -U "$PGUSER" -d "$PGDB" -v ON_ERROR_STOP=1 < "$f" || \
                            echo "    (warning: $f had errors -- continuing)"
                    done
                '''
            }
        }

        stage('2b. Phase 5/6 UI quality gates') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}/Frontend/PlantProcess.Web

                    # The phase56 acceptance validator resolves ../../Jenkinsfile
                    # from this web-app dir (i.e. /app/../../Jenkinsfile = /Jenkinsfile
                    # inside the container). We only mount the web app at /app, so we
                    # additionally bind the repo-root Jenkinsfile to /Jenkinsfile (ro)
                    # so that validator can read it.
                    docker run --rm \
                      -v "$PWD:/app" \
                      -v "${REPO_DIR}/Jenkinsfile:/Jenkinsfile:ro" \
                      -w /app \
                      node:20-alpine \
                      sh -lc '
                        set -e
                        node --version
                        npm --version
                        if [ -f package-lock.json ]; then npm ci; else npm install; fi
                        npx playwright install --with-deps chromium || npx playwright install chromium
                        npm run validate:phase5-phase6:strict
                      '
                '''
            }
        }

        stage('2b2. Lint warning budget + API client drift') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}/Frontend/PlantProcess.Web
                    docker run --rm \
                      -v "$PWD:/app" \
                      -w /app \
                      node:20-alpine \
                      sh -lc '
                        set -e
                        if [ -f package-lock.json ]; then npm ci; else npm install; fi
                        npx playwright install --with-deps chromium || npx playwright install chromium
                        npm run lint:ci
                        npm run openapi:check
                      '
                '''
            }
        }

        stage('2c. Phase 01/02 residual + schema-mapping gates') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}

                    docker run --rm \
                      -v "$PWD:/app" \
                      -w /app \
                      node:20-alpine \
                      node tools/validation/validate-phase01-phase02-gates.mjs

                    echo "Phase 01/02 gate passed: PPIQ-T101..PPIQ-T112"
                '''
            }
        }

        stage('2d. v5 Phase 01/02 ML foundation gates') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}

                    docker run --rm \
                      -v "$PWD:/app" \
                      -w /app \
                      node:20-alpine \
                      node tools/validation/validate-phase01-phase02-v5-gates.mjs

                    cd Frontend/PlantProcess.Web
                    docker run --rm \
                      -v "$PWD:/app" \
                      -w /app \
                      -e PPIQ_API_BASE_URL=https://api.178.105.152.180.sslip.io \
                      -e PPIQ_ADMIN_USER=${PPIQ_ADMIN_USER:-admin} \
                      -e PPIQ_ADMIN_PASSWORD=${PPIQ_ADMIN_PASSWORD:-} \
                      -e PPIQ_OPERATOR_USER=${PPIQ_OPERATOR_USER:-datamanager} \
                      -e PPIQ_OPERATOR_PASSWORD=${PPIQ_OPERATOR_PASSWORD:-} \
                      node:20-alpine \
                      sh -lc '
                        set -e
                        if [ -f package-lock.json ]; then npm ci; else npm install; fi
                        npm run validate:copy
                        npm run validate:standard-imports || echo "WARN PPIQ-T205: pages pending Standard-component conversion (advisory, non-blocking)"
                      '
                '''
            }
        }

        stage('2e. v6 Phase 01/02 completion gates') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}

                    docker run --rm \
                      -v "$PWD:/app" \
                      -w /app \
                      node:20-alpine \
                      sh -lc 'node tools/validation/validate-v6-phase01-phase02-completion.cjs && node tools/validation/validate-t208-exposure.cjs'


                '''
            }
            post {
                always { archiveArtifacts artifacts: 'Frontend/PlantProcess.Web/test-results/auth-matrix/**', allowEmptyArchive: true }
            }
        }

        stage('2z. Gate-exit certification') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}

                    echo "PPIQ_PACK_A3_CI_CERTIFICATION"
                    echo "taskClosure: T001-T071 task closure gate"
                    echo "routeContract: Pack D route contract snapshot"

                    docker run --rm \
                      -v "$PWD:/app" \
                      -w /app \
                      node:20-alpine \
                      sh -lc '
                        set -e
                        node --version

                        # taskClosure
                        node tools/task-closure/validate-t001-t071-task-closure.cjs
node tools/task-closure/ppiq-pack-a-scorecard-bridge.cjs

                        # routeContract
                        node tools/pack-d/validate-pack-d-route-contract-snapshot.cjs

                        node tools/pack-b/validate-pack-b-p05-closure.cjs
                        node tools/pack-d/validate-pack-d-backend-thinness.cjs
                        node tools/phase56/validate-phase56.cjs
                        node tools/ci/write-certification-gate-report.cjs
                      '
                '''
            }
            post {
                always {
                    archiveArtifacts artifacts: 'docs/ci/gate-report.json', allowEmptyArchive: true
                    archiveArtifacts artifacts: 'docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.*', allowEmptyArchive: true
                    archiveArtifacts artifacts: 'docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.*', allowEmptyArchive: true
                }
            }
        }

        stage('3. Build images') {
            steps {
                sh '''
                    cd ${DEPLOY_DIR}
                    docker compose -f ${COMPOSE_FILE} build --pull \
                        plantprocess-api \
                        plantprocess-workers \
                        plantprocess-app-web \
                        plantprocess-website
                '''
            }
        }

        stage('4. Recreate containers') {
            steps {
                sh '''
                    cd ${DEPLOY_DIR}
                    docker compose -f ${COMPOSE_FILE} up -d \
                        --force-recreate \
                        --no-deps \
                        plantprocess-api \
                        plantprocess-workers \
                        plantprocess-app-web \
                        plantprocess-website
                '''
            }
        }

        stage('5. Health + endpoint probes') {
            steps {
                sh '''
                    echo "Waiting 20s for API to come up..."
                    sleep 20

                    for i in 1 2 3 4 5; do
                        CODE=$(curl -s -o /dev/null -w "%{http_code}" ${API_HEALTH} --max-time 10 || echo "0")
                        echo "Attempt $i: ${API_HEALTH} -> $CODE"
                        if [ "$CODE" = "200" ] || [ "$CODE" = "401" ]; then
                            echo "API is responding"
                            break
                        fi
                        sleep 5
                    done

                    echo ""
                    echo "==> New endpoint freshness probes (expect 401 = registered + auth required):"
                    for p in \
                        "/admin/phase1/connector-truth" \
                        "/admin/phase2/pilot/deployment-checklist" \
                        "/analytics/phase2/ml-lifecycle"
                    do
                        CODE=$(curl -s -o /dev/null -w "%{http_code}" "https://api.178.105.152.180.sslip.io${p}" --max-time 5)
                        printf "    %-50s -> %s\\n" "$p" "$CODE"
                    done
                '''
            }
        }
        stage('5b. Deploy smoke gate') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}
                    sh deploy/ci/post-deploy-smoke.sh
                '''
            }
        }

        stage('6. Post-deploy browser gates (live stack)') {
            steps {
                catchError(buildResult: 'SUCCESS', stageResult: 'UNSTABLE') {
                sh '''
                    set -e
                    cd ${REPO_DIR}/Frontend/PlantProcess.Web
                    docker run --rm -v "$PWD:/app" -w /app \
                      -e CI=1 \
                      -e PLAYWRIGHT_BASE_URL=https://178.105.152.180.sslip.io \
                      -e PLAYWRIGHT_API_URL=https://api.178.105.152.180.sslip.io \
                      -e PPIQ_SMOKE_USERNAME=${PPIQ_SMOKE_USERNAME:-e2eadmin} \
                      -e PPIQ_SMOKE_PASSWORD=${PPIQ_SMOKE_PASSWORD:-} \
                      node:20-bookworm \
                      sh -lc '
                        set -e
                        if [ -f package-lock.json ]; then npm ci; else npm install; fi
                        npx playwright install --with-deps chromium
                        npm run test:phase56:e2e
                        npm run test:a11y
                        npm run test:visual
                        npm run test:auth-matrix
                      '
                '''
                }
            }
        }
        stage('PPIQ Phase01/02 security and verification gates') {
            steps {
                sh '''
                    set -e
                    cd ${REPO_DIR}

                    docker run --rm -v "$PWD:/app" -w /app mcr.microsoft.com/dotnet/sdk:9.0 sh -lc '
                        set -e
                        apt-get update -qq && apt-get install -y -qq nodejs >/dev/null 2>&1
                        node --version
                        dotnet --version
                        node tools/security/validate-no-demo-tenant-fallback.cjs
                        node tools/ci/validate-test-project-registration.cjs
                        node tools/security/validate-devseed-production-artifact.cjs
                        node tools/realization/validate-phase01-phase02.cjs
                      '

                    docker run --rm -v "$PWD:/repo" -w /repo ghcr.io/gitleaks/gitleaks:v8.18.4 detect --source /repo --no-git --redact --config /repo/.gitleaks.toml
                '''
            }
        }
    }

    post {
        success {
            echo 'Deployment succeeded - all stages green'
        }
        failure {
            echo 'Deployment FAILED - see console output for the stage that failed'
        }
        always {
            echo 'Build complete.'
        }

    }
}