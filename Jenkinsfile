pipeline {
    agent { label 'windows dotnet10' }
    
    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 1, unit: 'HOURS')
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        
        stage('Restore') {
            steps {
                bat 'dotnet restore'
            }
        }
        
        stage('Build') {
            steps {
                bat 'dotnet build -c Release'
            }
        }
        
        stage('Test') {
            steps {
                bat 'dotnet test --no-build -c Release --collect:"XPlat Code Coverage" --logger "trx;LogFileName=test-results.trx"'
            }
        }
    }
    
    post {
        always {
            // Parse test results
            junit testResults: '**/*.trx', allowEmptyResults: true
            
            // Archive coverage reports
            archiveArtifacts artifacts: '**/TestResults/**/coverage.cobertura.xml', fingerprint: true, allowEmptyArchive: true
            
            // Archive bin/Release folder
            archiveArtifacts artifacts: 'bin/Release/**/*', fingerprint: true
        }
        
        failure {
            echo 'Build FAILED!'
        }
        
        success {
            echo 'Build SUCCEEDED!'
        }
    }
}