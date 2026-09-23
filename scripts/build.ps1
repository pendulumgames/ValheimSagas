param(
 [string]$GameManaged = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed',
 [string]$BepInExCore = 'C:\Users\mecra\AppData\Roaming\com.kesomannen.gale\valheim\profiles\Test\BepInEx\core',
 [switch]$SkipTests,
 [string]$InventorySlotsDll = ''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Set-Location $root
& (Join-Path $PSScriptRoot 'stamp-web.ps1')
foreach($file in @((Join-Path $GameManaged 'assembly_valheim.dll'),(Join-Path $BepInExCore 'BepInEx.dll'))) { if(!(Test-Path -LiteralPath $file)){throw "Missing local game reference: $file"} }
dotnet build src/Sagas.Plugin/Sagas.Plugin.csproj -c Release -p:RestoreLockedMode=true "-p:GameManaged=$GameManaged" "-p:BepInExCore=$BepInExCore"
if($LASTEXITCODE -ne 0){throw 'Plugin build failed'}
if(!$SkipTests) {
 foreach($project in @('tests/Sagas.Tests','tests/Sagas.LoreTests','tests/Sagas.TerrainChecks','tests/Sagas.ArtChecks','tests/Sagas.JewelChecks')) {dotnet run --project $project -c Release;if($LASTEXITCODE -ne 0){throw "Failed: $project"}}
 dotnet run --project tests/Sagas.JewelChecks -c Release -- --absent
 if($LASTEXITCODE -ne 0){throw 'Absent Jewelcrafting adapter checks failed'}
 dotnet run --project tests/Sagas.ApiChecks -c Release -- $GameManaged (Join-Path (Split-Path $BepInExCore -Parent) 'plugins')
 if($LASTEXITCODE -ne 0){throw 'Installed API metadata checks failed'}
 if($InventorySlotsDll) {
  dotnet run --project tests/Sagas.InventoryChecks -c Release -- $InventorySlotsDll
  if($LASTEXITCODE -ne 0){throw 'Installed InventorySlots stacking checks failed'}
 }
 node --check src/Sagas.Web/app.js
 if($LASTEXITCODE -ne 0){throw 'Website JavaScript check failed'}
}
Write-Host 'Build and requested checks passed. Run scripts/package.ps1 to package.'
