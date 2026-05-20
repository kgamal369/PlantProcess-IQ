pipeline {
    agent any

    options {
        timestamps()
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '30', artifactNumToKeepStr: '10'))
        timeout(time: 45, unit: 'MINUTES')
    }

    environment {
        BACKEND_DIR = "${WORKSPACE}/Backend"
        APP_FRONTEND_DIR = "${WORKSPACE}/Frontend/PlantProcess.Web"
        WEBSITE_DIR = "${WORKSPACE}/Website/PlantProcess.Website"
        DEPLOY_DIR = "${WORKSPACE}/Infrastructure/deploy"
        CI_TOOLS_DIR = "${WORKSPACE}/tools/ci"
        CI = "true"

        COMPOSE_FILE = "docker-compose.demo.yml"

        // Internal service URLs inside the Docker Compose network
        PPIQ_INTERNAL_API_HEALTH_URL = "http://plantprocess-api:5063/health"
        PPIQ_INTERNAL_API_DB_HEALTH_URL = "http://plantprocess-api:5063/db-health"
        PPIQ_INTERNAL_APP_HEALTH_URL = "http://plantprocess-app-web/health"
        PPIQ_INTERNAL_WEBSITE_HEALTH_URL = "http://plantprocess-website/health"

        // Email notification. Update this in Jenkins/global env later if needed.
        PPIQ_CI_NOTIFY_EMAIL = "your-email@example.com"
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Prepare CI Context') {
            steps {
                script {
                    env.GIT_COMMIT_SHORT = sh(
                        script: "git rev-parse --short=12 HEAD",
                        returnStdout: true
                    ).trim()

                    env.PPIQ_IMAGE_TAG = "${env.BUILD_NUMBER}-${env.GIT_COMMIT_SHORT}"

                    echo "PlantProcess IQ CI/CD"
                    echo "Branch          : ${env.BRANCH_NAME}"
                    echo "Build Number    : ${env.BUILD_NUMBER}"
                    echo "Commit Short    : ${env.GIT_COMMIT_SHORT}"
                    echo "Image Tag       : ${env.PPIQ_IMAGE_TAG}"
                    echo "Workspace       : ${env.WORKSPACE}"
                }

                sh '''
                    echo "============================================================"
                    echo "Tool Versions"
                    echo "============================================================"
                    git --version
                    docker --version
                    docker compose version

                    chmod +x tools/ci/*.sh || true
                '''
            }
        }

        stage('Backend Restore + Build + Tests') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WORKSPACE}:/workspace" \
                      -w /workspace \
                      mcr.microsoft.com/dotnet/sdk:9.0 \
                      sh tools/ci/run-backend-ci.sh
                '''
            }

            post {
                always {
                    junit allowEmptyResults: true, testResults: 'Backend/**/TestResults/**/*.trx'
                    archiveArtifacts allowEmptyArchive: true, artifacts: 'Backend/**/TestResults/**/*'
                }
            }
        }

        stage('Frontend App Validate') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WORKSPACE}:/workspace" \
                      -w /workspace \
                      node:24-alpine \
                      sh tools/ci/run-frontend-ci.sh
                '''
            }

            post {
                always {
                    archiveArtifacts allowEmptyArchive: true, artifacts: 'Frontend/PlantProcess.Web/coverage/**/*,Frontend/PlantProcess.Web/test-results/**/*'
                }
            }
        }

        stage('Website Validate') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WORKSPACE}:/workspace" \
                      -w /workspace \
                      node:24-alpine \
                      sh tools/ci/run-website-ci.sh
                '''
            }
        }

        stage('Validate Deployment Configuration') {
            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Deployment Configuration Validation"
                        echo "============================================================"

                        test -f .env || {
                          echo "ERROR: Infrastructure/deploy/.env does not exist."
                          exit 1
                        }

                        test -f docker-compose.demo.yml || {
                          echo "ERROR: docker-compose.demo.yml does not exist."
                          exit 1
                        }

                        test -f Caddyfile || {
                          echo "ERROR: Caddyfile does not exist."
                          exit 1
                        }

                        docker compose -f docker-compose.demo.yml config >/tmp/ppiq-compose-config.yml

                        echo "Docker compose config is valid."
                    '''
                }
            }
        }

        stage('Build Docker Images') {
            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Docker Compose Build"
                        echo "Image tag: ${PPIQ_IMAGE_TAG}"
                        echo "============================================================"

                        export PPIQ_IMAGE_TAG="${PPIQ_IMAGE_TAG}"

                        docker compose -f docker-compose.demo.yml build postgres caddy jenkins plantprocess-api plantprocess-workers plantprocess-app-web plantprocess-website
                    '''
                }
            }
        }

        stage('Deploy Database Only') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    branch 'develop'
                    branch 'demo'
                }
            }

            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Deploy Database"
                        echo "============================================================"

                        docker compose -f docker-compose.demo.yml up -d postgres

                        echo "Waiting for PostgreSQL container health..."
                        for i in $(seq 1 30); do
                          STATUS="$(docker inspect --format='{{.State.Health.Status}}' ppiq-postgres 2>/dev/null || echo starting)"
                          echo "Postgres health: ${STATUS}"

                          if [ "$STATUS" = "healthy" ]; then
                            echo "Postgres is healthy."
                            exit 0
                          fi

                          sleep 3
                        done

                        echo "ERROR: PostgreSQL did not become healthy."
                        docker logs ppiq-postgres || true
                        exit 1
                    '''
                }
            }
        }

        stage('Apply EF Core Migrations') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    branch 'develop'
                    branch 'demo'
                }
            }

            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Apply EF Core Migrations"
                        echo "============================================================"

                        set -a
                        . ./.env
                        set +a

                        docker run --rm \
                          --network plantprocessiq-demo_ppiq-net \
                          -v "${WORKSPACE}/Backend:/workspace/Backend" \
                          -v "${WORKSPACE}/tools:/workspace/tools" \
                          -w /workspace/Backend \
                          -e "ConnectionStrings__PlantProcessDb=${ConnectionStrings__PlantProcessDb}" \
                          mcr.microsoft.com/dotnet/sdk:9.0 \
                          sh /workspace/tools/ci/run-ef-migrations.sh
                    '''
                }
            }
        }

        stage('Deploy Full Demo Stack') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    branch 'develop'
                    branch 'demo'
                }
            }

            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Deploy Full Stack"
                        echo "Image tag: ${PPIQ_IMAGE_TAG}"
                        echo "============================================================"

                        export PPIQ_IMAGE_TAG="${PPIQ_IMAGE_TAG}"

                        docker compose -f docker-compose.demo.yml up -d --remove-orphans

                        echo "Current compose status:"
                        docker compose -f docker-compose.demo.yml ps
                    '''
                }
            }
        }

        stage('Post-Deploy Smoke Tests') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    branch 'develop'
                    branch 'demo'
                }
            }

            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Post Deploy Smoke Tests"
                        echo "============================================================"

                        set -a
                        . ./.env
                        set +a

                        docker run --rm \
                          --network plantprocessiq-demo_ppiq-net \
                          -v "${WORKSPACE}/tools:/workspace/tools" \
                          -w /workspace \
                          curlimages/curl:8.11.1 \
                          sh tools/ci/post-deploy-smoke.sh
                    '''
                }
            }
        }

        stage('Post-Deploy E2E Smoke') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    branch 'develop'
                    branch 'demo'
                }
            }

            steps {
                sh '''
                    echo "============================================================"
                    echo "Post Deploy Playwright Smoke"
                    echo "============================================================"

                    docker run --rm \
                      --network plantprocessiq-demo_ppiq-net \
                      -v "${APP_FRONTEND_DIR}:/app" \
                      -w /app \
                      -e CI=true \
                      -e PLAYWRIGHT_BASE_URL=http://plantprocess-app-web \
                      -e PLAYWRIGHT_API_URL=http://plantprocess-api:5063 \
                      mcr.microsoft.com/playwright:v1.60.0-noble \
                      sh -lc "npm ci && npx playwright test e2e/route-smoke.spec.ts e2e/p0-auth-pages-contract.spec.ts --reporter=list"
                '''
            }

            post {
                always {
                    archiveArtifacts allowEmptyArchive: true, artifacts: 'Frontend/PlantProcess.Web/playwright-report/**/*,Frontend/PlantProcess.Web/test-results/**/*'
                }
            }
        }

        stage('Record Successful Image Tag') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    branch 'develop'
                    branch 'demo'
                }
            }

            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        echo "============================================================"
                        echo "Record Successful Image Tag"
                        echo "============================================================"

                        set -a
                        . ./.env
                        set +a

                        TAG_FILE="${PPIQ_LAST_SUCCESSFUL_TAG_FILE:-/data/plantprocess-iq/last-successful-image-tag.txt}"

                        mkdir -p "$(dirname "$TAG_FILE")"
                        echo "${PPIQ_IMAGE_TAG}" > "$TAG_FILE"

                        echo "Recorded successful tag: ${PPIQ_IMAGE_TAG}"
                        cat "$TAG_FILE"
                    '''
                }
            }
        }
    }

    post {
        success {
            echo 'PlantProcess IQ CI/CD completed successfully.'

            emailext(
                to: "${PPIQ_CI_NOTIFY_EMAIL}",
                subject: "SUCCESS: PlantProcess IQ CI/CD #${BUILD_NUMBER}",
                body: """
PlantProcess IQ CI/CD succeeded.

Build: ${BUILD_NUMBER}
Branch: ${BRANCH_NAME}
Commit: ${GIT_COMMIT_SHORT}
Image Tag: ${PPIQ_IMAGE_TAG}
Job: ${JOB_NAME}
URL: ${BUILD_URL}
"""
            )
        }

        failure {
            echo 'PlantProcess IQ CI/CD failed. Attempting rollback if this was a deploy branch.'

            script {
                if (env.BRANCH_NAME == 'main' || env.BRANCH_NAME == 'master' || env.BRANCH_NAME == 'develop' || env.BRANCH_NAME == 'demo') {
                    sh '''
                        echo "============================================================"
                        echo "Rollback Attempt"
                        echo "============================================================"

                        cd "${DEPLOY_DIR}"

                        set -a
                        . ./.env
                        set +a

                        TAG_FILE="${PPIQ_LAST_SUCCESSFUL_TAG_FILE:-/data/plantprocess-iq/last-successful-image-tag.txt}"

                        if [ ! -f "$TAG_FILE" ]; then
                          echo "No previous successful image tag found. Rollback skipped."
                          exit 0
                        fi

                        PREVIOUS_TAG="$(cat "$TAG_FILE" | tr -d '[:space:]')"

                        if [ -z "$PREVIOUS_TAG" ]; then
                          echo "Previous successful tag file is empty. Rollback skipped."
                          exit 0
                        fi

                        echo "Rolling back to image tag: $PREVIOUS_TAG"

                        export PPIQ_IMAGE_TAG="$PREVIOUS_TAG"

                        docker compose -f docker-compose.demo.yml up -d --remove-orphans

                        echo "Rollback compose status:"
                        docker compose -f docker-compose.demo.yml ps
                    '''
                } else {
                    echo "Branch ${env.BRANCH_NAME} is not a deploy branch. Rollback skipped."
                }
            }

            emailext(
                to: "${PPIQ_CI_NOTIFY_EMAIL}",
                subject: "FAILED: PlantProcess IQ CI/CD #${BUILD_NUMBER}",
                body: """
PlantProcess IQ CI/CD failed.

Build: ${BUILD_NUMBER}
Branch: ${BRANCH_NAME}
Commit: ${GIT_COMMIT_SHORT}
Image Tag: ${PPIQ_IMAGE_TAG}
Job: ${JOB_NAME}
URL: ${BUILD_URL}

Rollback was attempted if this branch is deploy-enabled.
Please review the Jenkins console output.
"""
            )
        }

        unstable {
            echo 'PlantProcess IQ CI/CD is unstable.'

            emailext(
                to: "${PPIQ_CI_NOTIFY_EMAIL}",
                subject: "UNSTABLE: PlantProcess IQ CI/CD #${BUILD_NUMBER}",
                body: """
PlantProcess IQ CI/CD is unstable.

Build: ${BUILD_NUMBER}
Branch: ${BRANCH_NAME}
Job: ${JOB_NAME}
URL: ${BUILD_URL}
"""
            )
        }

        always {
            cleanWs(
                deleteDirs: true,
                disableDeferredWipeout: true,
                notFailBuild: true
            )
        }
    }
}