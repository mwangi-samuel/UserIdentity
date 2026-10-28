$composeArguments = @('--project-directory', $PSScriptRoot, '-f', (Join-Path $PSScriptRoot 'docker-compose.yml'))

docker compose @composeArguments down
exit $LASTEXITCODE
