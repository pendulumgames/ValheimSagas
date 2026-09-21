param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [string]$OutputDirectory = '',
    [ValidateSet('Run','RunHttp','RunStartup')][string]$Method = 'Run'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectRoot ('.dev/mono-check-' + [guid]::NewGuid().ToString('N'))
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory; existing data will not be overwritten.' }
dotnet build (Join-Path $projectRoot 'tests/Sagas.IncidentChecks/Sagas.MonoIncident.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Mono probe build failed.' }
$previousOutput = $env:SAGAS_INCIDENT_OUTPUT
try {
    $env:SAGAS_INCIDENT_OUTPUT = $OutputDirectory
    python (Join-Path $PSScriptRoot 'mono-probe.py') (Join-Path $projectRoot 'tests/Sagas.IncidentChecks/bin/Release/netstandard2.1/Sagas.MonoIncident.dll') --game $GamePath --method $Method
    if ($LASTEXITCODE -ne 0) { throw "Unity Mono probe failed. Inspect $OutputDirectory/report.txt" }
    Get-Content -LiteralPath (Join-Path $OutputDirectory 'report.txt')
} finally {
    $env:SAGAS_INCIDENT_OUTPUT = $previousOutput
}
