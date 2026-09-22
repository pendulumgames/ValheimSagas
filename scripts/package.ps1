param([string]$Version='0.3.26')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
$binary=Join-Path $root 'src/Sagas.Plugin/bin/Release/netstandard2.1'
if(!(Test-Path (Join-Path $binary 'ValheimSagas.dll'))){throw 'Run scripts/build.ps1 first.'}
. (Join-Path $PSScriptRoot 'web-artwork.ps1')
$artSizes=@{}
foreach($asset in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'web-assets.txt') | Where-Object {$_ -like '*.webp'})){
 $stream=[IO.File]::OpenRead((Join-Path $root "src/Sagas.Web/$asset"))
 try{$artSizes[$asset]=Get-WebPSize $stream}finally{$stream.Dispose()}
}
Assert-WebArtworkSizes $artSizes
$artifacts=Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$stage=Join-Path $artifacts ('package-'+[Guid]::NewGuid().ToString('N'))
$plugin=Join-Path $stage 'BepInEx/plugins/ValheimSagas'
New-Item -ItemType Directory -Force (Join-Path $plugin 'web') | Out-Null
# Explicit allow-list. Never package game/Unity/BepInEx references, saves, development databases or configuration.
foreach($dll in @('ValheimSagas.dll','Sagas.Core.dll','LiteDB.dll','Newtonsoft.Json.dll')) {
 $source=Join-Path $binary $dll
 if(!(Test-Path $source)){throw "Missing package dependency $dll. Enable CopyLocalLockFileAssemblies in plugin project."}
 Copy-Item -LiteralPath $source -Destination $plugin
}
foreach($asset in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'web-assets.txt'))){
 if(!$asset.Trim()){continue}
 $destination=Join-Path $plugin ('web/'+$asset)
 New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
 Copy-Item -LiteralPath (Join-Path $root "src/Sagas.Web/$asset") -Destination $destination
}
foreach($doc in (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-docs.txt') | Where-Object {$_.Trim()})){
 $source=if($doc -eq 'README.md'){'docs/MOD-README.md'}else{$doc}
 $destination=Join-Path $stage $doc
 New-Item -ItemType Directory -Force (Split-Path $destination -Parent) | Out-Null
 Copy-Item -LiteralPath (Join-Path $root $source) -Destination $destination
}
$manifest=[ordered]@{name='ValheimSagas';version_number=$Version;website_url='https://github.com/pendulumgames/ValheimSagas';description='Every Viking has a story. Standalone server website with player statistics, a 2D world atlas, character armory, leaderboards and evolving sagas.';dependencies=@('denikson-BepInExPack_Valheim-5.4.2350')}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding UTF8
Get-ChildItem $stage -File -Recurse | ForEach-Object {"$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($_.FullName.Substring($stage.Length+1).Replace('\','/'))"} | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ASCII
$zip=Join-Path $artifacts "ValheimSagas-$Version.zip"
Add-Type -AssemblyName System.IO.Compression
$stream=[IO.File]::Open($zip,[IO.FileMode]::Create,[IO.FileAccess]::Write)
$archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
try {
 foreach($file in Get-ChildItem $stage -File -Recurse) {
  $relative=$file.FullName.Substring($stage.Length+1).Replace('\','/')
  $entry=$archive.CreateEntry($relative,[IO.Compression.CompressionLevel]::Optimal)
  $inputStream=$file.OpenRead();$outputStream=$entry.Open()
  try{$inputStream.CopyTo($outputStream)}finally{$inputStream.Dispose();$outputStream.Dispose()}
 }
} finally {$archive.Dispose();$stream.Dispose()}
Write-Host "Created $zip"
((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ASCII
# Retain stage for local review; no destructive cleanup and no publication.
