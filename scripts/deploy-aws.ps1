param(
    [string]$StackName = 'cloudledger-dev',
    [string]$EnvironmentName = 'dev',
    [string]$Region = 'us-east-1',
    [string]$DbUsername = 'cloudledger'
)

$ErrorActionPreference = 'Stop'

function Assert-LastExitCode {
    if ($LASTEXITCODE -ne 0) {
        throw "Comando externo falhou com código $LASTEXITCODE."
    }
}

function Get-StackOutput {
    param([string]$OutputKey)

    $query = "Stacks[0].Outputs[?OutputKey=='$OutputKey'].OutputValue"
    $value = aws cloudformation describe-stacks `
        --stack-name $StackName `
        --region $Region `
        --query $query `
        --output text
    Assert-LastExitCode

    if (-not $value -or $value -eq 'None') {
        throw "Output $OutputKey não encontrado na stack $StackName."
    }

    return $value.Trim()
}

$dbPassword = $env:CLOUDLEDGER_DB_PASSWORD
$jwtKey = $env:CLOUDLEDGER_JWT_KEY
$mongoUri = $env:CLOUDLEDGER_MONGO_URI
$alarmEmail = $env:CLOUDLEDGER_ALARM_EMAIL

foreach ($item in @(
    @{ Name = 'CLOUDLEDGER_DB_PASSWORD'; Value = $dbPassword },
    @{ Name = 'CLOUDLEDGER_JWT_KEY'; Value = $jwtKey },
    @{ Name = 'CLOUDLEDGER_MONGO_URI'; Value = $mongoUri }
)) {
    if ([string]::IsNullOrWhiteSpace($item.Value)) {
        throw "Defina a variável de ambiente $($item.Name) antes de executar o deploy."
    }
}

if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw 'AWS CLI não encontrada no PATH.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker não encontrado no PATH.'
}

$template = Join-Path $PSScriptRoot '..\infrastructure\cloudformation\cloudledger.yaml'
if (-not (Test-Path $template)) {
    throw "Template não encontrado: $template"
}

$overrides = @(
    "EnvironmentName=$EnvironmentName",
    "DbUsername=$DbUsername",
    "DbPassword=$dbPassword",
    "JwtSigningKey=$jwtKey",
    "MongoConnectionString=$mongoUri"
)

if (-not [string]::IsNullOrWhiteSpace($alarmEmail)) {
    $overrides += "AlarmEmail=$alarmEmail"
}

Write-Host '==> Provisionando/atualizando a stack CloudFormation'
aws cloudformation deploy `
    --stack-name $StackName `
    --template-file $template `
    --region $Region `
    --capabilities CAPABILITY_NAMED_IAM `
    --parameter-overrides @overrides `
    --no-fail-on-empty-changeset
Assert-LastExitCode

$accountId = aws sts get-caller-identity --region $Region --query Account --output text
Assert-LastExitCode
$registry = "$accountId.dkr.ecr.$Region.amazonaws.com"
$apiRepository = Get-StackOutput 'ApiRepositoryName'
$workerRepository = Get-StackOutput 'WorkerRepositoryName'
$frontendBucket = Get-StackOutput 'FrontendBucketName'
$distributionId = Get-StackOutput 'FrontendDistributionId'
$cluster = "$EnvironmentName-cloudledger"

Write-Host '==> Publicando imagens no ECR'
aws ecr get-login-password --region $Region | docker login --username AWS --password-stdin $registry
Assert-LastExitCode

$apiImage = "$registry/$($apiRepository):latest"
$workerImage = "$registry/$($workerRepository):latest"

docker build -f backend/CloudLedger.Api/Dockerfile -t $apiImage .
Assert-LastExitCode
docker push $apiImage
Assert-LastExitCode

docker build -f backend/CloudLedger.Worker/Dockerfile -t $workerImage .
Assert-LastExitCode
docker push $workerImage
Assert-LastExitCode

Write-Host '==> Gerando e publicando o frontend'
Push-Location frontend/cloudledger-web
try {
    npm ci
    Assert-LastExitCode
    npm run build -- --configuration production
    Assert-LastExitCode
}
finally {
    Pop-Location
}

aws s3 sync frontend/cloudledger-web/dist/cloudledger-web/browser "s3://$frontendBucket" --delete
Assert-LastExitCode
aws cloudfront create-invalidation --distribution-id $distributionId --paths '/*' --region $Region
Assert-LastExitCode

Write-Host '==> Forçando novo deployment dos serviços ECS'
aws ecs update-service --cluster $cluster --service "$EnvironmentName-cloudledger-api" --force-new-deployment --region $Region
Assert-LastExitCode
aws ecs update-service --cluster $cluster --service "$EnvironmentName-cloudledger-worker" --force-new-deployment --region $Region
Assert-LastExitCode

Write-Host 'Deploy concluído.'
Write-Host "Frontend: $(Get-StackOutput 'FrontendUrl')"
Write-Host "API:      $(Get-StackOutput 'ApiUrl')"
