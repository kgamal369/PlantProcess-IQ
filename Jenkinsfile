// ============================================================
// PlantProcess IQ — CI/CD pipeline
//
// What this does on every git push to main:
//   1. Pull latest source on the deploy server (/opt/PlantProcess-IQ)
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
        DEPLOY_DIR   = '/opt/PlantProcess-IQ/Infrastructure/deploy'
        COMPOSE_FILE = 'docker-compose.demo.yml'
        DB_CONTAINER = 'ppiq-postgres'
        API_HEALTH   = 'https://api.178.105.152.180.sslip.io/health'
    }

    stages {
        stage('1. Pull latest code') {
            steps {
                sh '''
                    cd ${REPO_DIR}
                    git fetch --all --prune
                    git checkout main
                    git pull --ff-only
                    echo "==> HEAD is now at:"
                    git log -1 --format='%h %ai %s'
                '''
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
                        docker exec -i ${DB_CONTAINER} psql -U "$PGUSER" -d "$PGDB" -v ON_ERROR_STOP=0 < "$f" || \
                            echo "    (warning: $f had errors -- continuing)"
                    done
                '''
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
    }

    post {
        success {
            echo '✓ Deployment succeeded — all stages green'
        }
        failure {
            echo '✗ Deployment FAILED — see console output for the stage that failed'
        }
        always {
            echo 'Build complete.'
        }
    }
}
