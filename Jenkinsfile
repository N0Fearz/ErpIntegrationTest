pipeline {
  agent any
  tools {
    dotnetsdk 'SDK 8'
  }
  environment {
      DOTNET_SYSTEM_GLOBALIZATION_INVARIANT = "true"
      PATH = "${env.HOME}/.dotnet/tools:${env.PATH}" // Voeg ~/.dotnet/tools toe aan het PATH
  }
  stages{
    stage('Clean and checkout'){
      steps{
        cleanWs()
        checkout scm
      }
    }
      
    stage('Test'){
      steps{
        sh 'dotnet test --configuration Release'
      }
    }
  }
}