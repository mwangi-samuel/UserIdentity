$exitCode = 0
Push-Location $PSScriptRoot

try {
    $solution = @(Get-ChildItem -Filter *.slnx)[0].Name

    $steps = @(
        { dotnet tool restore },
        { dotnet format style $solution },
        { dotnet format analyzers $solution },
        { dotnet csharpier format . },
        { dotnet build $solution -c Release -clp:ErrorsOnly }
    )

    foreach ($step in $steps) {
        Write-Host "> $($step.ToString().Trim())"
        & $step

        if ($LASTEXITCODE -ne 0) {
            $exitCode = $LASTEXITCODE
            break
        }
    }
}
finally {
    Pop-Location
}

exit $exitCode
