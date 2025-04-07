library 'JenkinsBuilderLibrary'

helper.gitHubUsername = 'jakegough-jaytwo'
helper.gitHubRepository = 'jaytwo.NuGetCheck'
helper.gitHubTokenCredentialsId = 'github-jakegough-jaytwo-token'
helper.xunitTestResultsPattern = 'out/testResults/**/*.trx'
helper.coberturaCoverageReport = 'out/coverage/Cobertura.xml';
helper.htmlCoverageReportDir = 'out/coverage/html';

def nuGetCredentialsId = 'nuget-org-jaytwo'

helper.run('linux && make && docker', {
    def timestamp = helper.getTimestamp()
    def safeJobName = helper.getSafeJobName()
    def dockerLocalTag = "jenkins__${safeJobName}__${timestamp}"
    def dockerBuilderTag = dockerLocalTag + "__builder"

    withEnv(["DOCKER_TAG=${dockerLocalTag}", "TIMESTAMP=${timestamp}"]) {
        try {
            stage ('Build') {
                sh "make docker-builder"
            }
            docker.image(dockerBuilderTag).inside() {
                stage ('Unit Test') {
                    sh "make unit-test"
                }
                stage ('Pack') {
                    if(env.BRANCH_NAME == 'master'){
                        sh "make pack"
                    } else {
                        sh "make pack-beta"
                    }
                }
                if(env.BRANCH_NAME == 'master' || env.BRANCH_NAME == 'develop'){
                    withCredentials([string(credentialsId: nuGetCredentialsId, variable: "NUGET_API_KEY")]) {
                        stage ('NuGet Check Version') {
                            sh "make nuget-check"
                        }
                        stage ('NuGet Push') {
                            sh "make nuget-push"
                        }
                    }
                }
            }
        }
        finally {
            // inside the withEnv()
            sh "make docker-copy-from-builder-output"
            sh "make docker-clean"
        }
    }
})
