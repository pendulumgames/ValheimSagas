param([string]$Version='0.3.36')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$app=Join-Path $root 'src/Sagas.Web/app.js'
$source=[IO.File]::ReadAllText($app)
if(!$source.Contains("const WEBSITE_VERSION='$Version';")){throw 'Executing website version does not match requested release'}
$plugin=[IO.File]::ReadAllText((Join-Path $root 'src/Sagas.Plugin/SagasPlugin.cs'))
if(!$plugin.Contains('"Valheim Sagas", "'+$Version+'"')){throw 'Plugin version does not match website release'}
$hash=(Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash.ToLowerInvariant()
$json=[ordered]@{version=$Version;appSha256=$hash}|ConvertTo-Json -Compress
[IO.File]::WriteAllText((Join-Path $root 'src/Sagas.Web/version.json'),$json,[Text.UTF8Encoding]::new($false))
Write-Host "Stamped website $Version"
