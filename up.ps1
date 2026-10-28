param(
  [switch]$UseLocal,
  [switch]$Build
)

$composeArguments = @('--project-directory', $PSScriptRoot, '-f', (Join-Path $PSScriptRoot 'docker-compose.yml'))

if ($UseLocal) {

  # --wait returns once the db and redpanda health checks pass
  docker compose @composeArguments up -d --wait --scale useridentity=0
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  dotnet run --project (Join-Path $PSScriptRoot 'UserIdentity/UserIdentity.csproj') --launch-profile UserIdentity
}
elseif ($Build) {

  docker compose @composeArguments up --build -d
}
else {

  docker compose @composeArguments up -d
}
