param(
  [switch]$UseLocal,
  [switch]$Build
)

$composeArguments = @('--project-directory', $PSScriptRoot, '-f', (Join-Path $PSScriptRoot 'docker-compose.yml'))

$keyFolder = Join-Path $HOME '.local-eddsa-keys'
$keyPassPhrase = 'TestKeyPassPhrase-For-Local-Use-Only'
$opensslImage = 'alpine/openssl:3.5.8'

$serviceUrl = 'http://localhost:5050'
$headers = @{ 'X-Api-Key' = 'TestApiKey-For-Local-Use-Only-0123456789'; 'X-App-Name' = 'testapp' }

$admins = @(
  @{ UserName = 'admin'; Password = 'Adm1n@P@ss'; FirstName = 'Admin'; LastName = 'User'; Email = 'admin@example.com'; Grant = "@app_name='testapp'" },
  @{ UserName = 'globaladmin'; Password = 'Gl0b@lAdm1n'; FirstName = 'Global'; LastName = 'Admin'; Email = 'globaladmin@example.com'; Grant = "@role_name='global:admin'" }
)

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

function Add-LocalAdmins {
  $grantScript = Get-Content -Raw (Join-Path $PSScriptRoot '.sql/add_admin_user_role.sql')

  foreach ($admin in $admins) {
    $login = @{ userName = $admin.UserName; password = $admin.Password } | ConvertTo-Json

    try {
      Invoke-RestMethod -Method Post -ErrorAction Stop -Uri "$serviceUrl/api/v1/user/login" -Headers $headers -ContentType 'application/json' -Body $login | Out-Null
    }
    catch {
      $registration = @{
        firstName    = $admin.FirstName
        lastName     = $admin.LastName
        userName     = $admin.UserName
        userEmail    = $admin.Email
        userPassword = $admin.Password
      } | ConvertTo-Json

      Invoke-RestMethod -Method Post -ErrorAction Stop -Uri "$serviceUrl/api/v1/user/register" -Headers $headers -ContentType 'application/json' -Body $registration | Out-Null
    }

    $grantScript | docker compose @composeArguments exec -T db mysql -uroot -proot "--init-command=SET @username='$($admin.UserName)', $($admin.Grant)" | Out-Null
    if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
    }
  }

  Write-Host 'Admins ready: admin (Adm1n@P@ss) is testapp:admin and globaladmin (Gl0b@lAdm1n) is global:admin.'
}

Import-LocalKeyPair

if ($UseLocal) {

  # --wait returns once the db and nats health checks pass
  docker compose @composeArguments up -d --wait --scale useridentity=0
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  $service = Start-Process dotnet -NoNewWindow -PassThru -ArgumentList @(
    'run', '--project', (Join-Path $PSScriptRoot 'UserIdentity/UserIdentity.csproj'), '--launch-profile', 'UserIdentity'
  )
  $null = $service.Handle

  $deadline = (Get-Date).AddMinutes(3)
  $healthy = $false

  while (-not $healthy -and -not $service.HasExited -and (Get-Date) -lt $deadline) {
    try {
      $healthy = (Invoke-WebRequest -UseBasicParsing -ErrorAction Stop -TimeoutSec 5 "$serviceUrl/health").StatusCode -eq 200
    }
    catch {
      Start-Sleep -Seconds 2
    }
  }

  if ($healthy) {
    Add-LocalAdmins
  }
  else {
    Write-Warning 'UserIdentity did not become healthy, so the admins were not set up.'
  }

  $service.WaitForExit()
  exit $service.ExitCode
}
else {

  $buildArguments = @(if ($Build) { '--build' })

  docker compose @composeArguments up -d --wait @buildArguments
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }

  Add-LocalAdmins
}
