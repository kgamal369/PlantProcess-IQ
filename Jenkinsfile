pipeline {
    agent any

    options {
        timestamps()
        disableConcurrentBuilds()
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    environment {
        BACKEND_DIR = "${WORKSPACE}/Backend"
        APP_FRONTEND_DIR = "${WORKSPACE}/Frontend/PlantProcess.Web"
        WEBSITE_DIR = "${WORKSPACE}/Website/PlantProcess.Website"
        DEPLOY_DIR = "${WORKSPACE}/Infrastructure/deploy"
        CI = "true"
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Context') {
            steps {
                sh '''
                    echo "Workspace: ${WORKSPACE}"
                    git --version
                    docker --version
                    docker compose version
                '''
            }
        }

        stage('Backend Restore') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WORKSPACE}:/workspace" \
                      -w /workspace/Backend \
                      mcr.microsoft.com/dotnet/sdk:9.0 \
                      dotnet restore PlantProcessIQ.sln
                '''
            }
        }

        stage('Backend Build') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WORKSPACE}:/workspace" \
                      -w /workspace/Backend \
                      mcr.microsoft.com/dotnet/sdk:9.0 \
                      dotnet build PlantProcessIQ.sln -c Release --no-restore
                '''
            }
        }

        stage('Backend Tests') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WORKSPACE}:/workspace" \
                      -w /workspace/Backend \
                      mcr.microsoft.com/dotnet/sdk:9.0 \
                      dotnet test PlantProcessIQ.sln -c Release --no-build
                '''
            }
        }

        stage('App Frontend Validate') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${APP_FRONTEND_DIR}:/app" \
                      -w /app \
                      node:24-alpine \
                      sh -lc "npm ci && npm run build && npm run lint && npm run test && npm run language:audit"
                '''
            }
        }

        stage('Website Build') {
            steps {
                sh '''
                    docker run --rm \
                      -v "${WEBSITE_DIR}:/app" \
                      -w /app \
                      node:24-alpine \
                      sh -lc "npm ci && npm run build"
                '''
            }
        }

        stage('Docker Compose Build') {
            steps {
                dir("${DEPLOY_DIR}") {
                    sh '''
                        test -f .env
                        docker compose -f docker-compose.demo.yml build
                    '''
                }
            }
        }

        stage('Deploy Demo Stack') {
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
                        docker compose -f docker-compose.demo.yml up -d --remove-orphans
                        docker compose -f docker-compose.demo.yml ps
                    '''
                }
            }
        }
    }

    post {
        success {
            echo 'PlantProcess IQ CI/CD completed successfully.'
        }

        failure {
            echo 'PlantProcess IQ CI/CD failed.'
        }
    }
}