param(
  [switch]$UseLocal,
  [switch]$Build
)

$composeArguments = @('--project-directory', $PSScriptRoot, '-f', (Join-Path $PSScriptRoot 'docker-compose.yml'))

$keyFolder = Join-Path $HOME '.local-eddsa-keys'
$keyPassPhrase = 'TestKeyPassPhrase-For-Local-Use-Only'
$opensslImage = 'alpine/openssl:3.5.8'

function Import-LocalKeyPair {
  $privateKeyPath = Join-Path $keyFolder 'private.pem'
  $publicKeyPath = Join-Path $keyFolder 'public.pem'

  if (-not (Test-Path $privateKeyPath)) {
    $privateKey = (docker run --rm $opensslImage genpkey -algorithm ED25519 -aes-256-cbc -pass "pass:$keyPassPhrase") -join "`n"
    if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
    }

    $publicKey = ($privateKey | docker run --rm -i $opensslImage pkey -pubout -passin "pass:$keyPassPhrase") -join "`n"
    if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
    }

    New-Item -ItemType Directory -Force $keyFolder | Out-Null
    [IO.File]::WriteAllText($publicKeyPath, "$publicKey`n")
    [IO.File]::WriteAllText($privateKeyPath, "$privateKey`n")
  }

  $env:EDDSA_PRIVATE_KEY = [IO.File]::ReadAllText($privateKeyPath)
  $env:EDDSA_PUBLIC_KEY = [IO.File]::ReadAllText($publicKeyPath)
}

Import-LocalKeyPair

if ($UseLocal) {

  # --wait returns once the db and nats health checks pass
  docker compose @composeArguments up -d --wait --scale useridentity=0
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  $env:CodeCheckSettings__VerifyUrl = 'http://localhost:1080/api/v1/message/verify'
  $env:CodeCheckSettings__ResendUrl = 'http://localhost:1080/api/v1/message/retry'

  dotnet run --project (Join-Path $PSScriptRoot 'UserIdentity/UserIdentity.csproj') --launch-profile UserIdentity
  exit $LASTEXITCODE
}
else {

  $buildArguments = @(if ($Build) { '--build' })

  docker compose @composeArguments up -d --wait @buildArguments
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  Write-Host 'The bootstrap admin from appsettings.json, admin@example.com, is the global admin and an admin of TestApp.'
}
