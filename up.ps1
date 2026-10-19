param(
  [switch]$UseLocal,
  [switch]$Build
)

if ($UseLocal) {

  # --wait returns once the db and redpanda health checks pass
  docker compose -f docker-compose.yml up -d --wait --scale useridentity=0
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  dotnet run --project .\UserIdentity\UserIdentity.csproj --launch-profile UserIdentity
}
elseif ($Build) {

  docker compose -f docker-compose.yml up --build -d
}
else {

  docker compose -f docker-compose.yml up -d
}
