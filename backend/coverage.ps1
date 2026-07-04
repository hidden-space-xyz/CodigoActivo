#!/usr/bin/env pwsh
# Runs the full backend test suite with coverage and prints a production-code summary.
# Requires the reportgenerator global tool:  dotnet tool install --global dotnet-reportgenerator-globaltool
# Usage:  ./coverage.ps1            (build + test + report)
#         ./coverage.ps1 -NoBuild   (reuse the last build)
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Remove-Item -Recurse -Force ./TestResults, ./coveragereport -ErrorAction SilentlyContinue

$testArgs = @('test', 'CodigoActivo.slnx',
    '--collect:XPlat Code Coverage', '--settings', 'coverlet.runsettings',
    '--results-directory', './TestResults')
if ($NoBuild) { $testArgs += '--no-build' }
dotnet @testArgs

reportgenerator `
    "-reports:./TestResults/**/coverage.cobertura.xml" `
    "-targetdir:./coveragereport" `
    "-reporttypes:TextSummary;Html" `
    "-assemblyfilters:+CodigoActivo.*;-CodigoActivo.UnitTests;-CodigoActivo.IntegrationTests" `
    "-classfilters:-CodigoActivo.Infrastructure.Database.Migrations.*"

Get-Content ./coveragereport/Summary.txt

# Fail the script if line coverage drops below the maintained floor.
$line = (Select-String -Path ./coveragereport/Summary.txt -Pattern 'Line coverage:\s*([0-9.]+)%').Matches[0].Groups[1].Value
if ([double]$line -lt 95) {
    Write-Error "Line coverage $line% is below the 95% floor."
    exit 1
}
Write-Host "Line coverage $line% (>= 95% floor)." -ForegroundColor Green
