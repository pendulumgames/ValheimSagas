param([string]$Path = 'artifacts/ValheimSagas-0.3.14.zip')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(![IO.Path]::IsPathRooted($Path)){$Path=Join-Path $root $Path}
Add-Type -AssemblyName System.IO.Compression.FileSystem
. (Join-Path $PSScriptRoot 'web-artwork.ps1')
$resolved=(Resolve-Path -LiteralPath $Path).Path
$zip=[IO.Compression.ZipFile]::OpenRead($resolved)
function Read-EntryText([string]$Name){
 $entry=$zip.GetEntry($Name);if(!$entry){throw "Missing entry $Name"}
 $reader=[IO.StreamReader]::new($entry.Open());try{return $reader.ReadToEnd()}finally{$reader.Dispose()}
}
try {
 $prefix='BepInEx/plugins/ValheimSagas/'
 $dlls=@('ValheimSagas.dll','Sagas.Core.dll','LiteDB.dll','Newtonsoft.Json.dll')
 $webAssets=@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'web-assets.txt') | Where-Object {$_.Trim()})
 $docs=@(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package-docs.txt') | Where-Object {$_.Trim()})
 $expected=@('manifest.json','SHA256SUMS.txt') + $docs + @($dlls | ForEach-Object {$prefix+$_}) + @($webAssets | ForEach-Object {$prefix+'web/'+$_})
 $actual=@($zip.Entries | ForEach-Object {$_.FullName})
 $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($name in $actual){
  if(!$seen.Add($name)){throw "Duplicate archive path: $name"}
  if($name -match '(^/|\\|:|(^|/)\.\.?(/|$)|[\x00-\x1f])'){throw "Nonportable archive path: $name"}
 }
 if(@(Compare-Object $expected $actual).Count){throw 'Unexpected or missing archive file; exact release allowlist required'}
 $manifest=(Read-EntryText 'manifest.json') | ConvertFrom-Json
 $fields=@($manifest.PSObject.Properties.Name)
 if(@(Compare-Object @('name','version_number','website_url','description','dependencies') $fields).Count){throw 'Manifest must contain exactly the five required fields'}
 if($manifest.name -cne 'ValheimSagas' -or $manifest.version_number -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package name/version'}
 if([IO.Path]::GetFileName($resolved) -cne "ValheimSagas-$($manifest.version_number).zip"){throw 'Archive filename and manifest version disagree'}
 if($manifest.website_url -cne 'https://github.com/pendulumgames/ValheimSagas'){throw 'Unexpected source repository URL'}
 if(!$manifest.description -or $manifest.description.Length -gt 250){throw 'Description must contain 1 to 250 characters'}
 if(@($manifest.dependencies).Count -ne 1 -or $manifest.dependencies[0] -cne 'denikson-BepInExPack_Valheim-5.4.2350'){throw 'Unexpected dependency list'}
 $icon=$zip.GetEntry('icon.png').Open()
 try{
  $header=New-Object byte[] 24
  $offset=0;while($offset -lt 24){$read=$icon.Read($header,$offset,24-$offset);if(!$read){throw 'Truncated icon PNG'};$offset+=$read}
  if([BitConverter]::ToString($header,0,8) -ne '89-50-4E-47-0D-0A-1A-0A'){throw 'Icon must be PNG'}
  if([BitConverter]::ToString($header,16,8) -ne '00-00-01-00-00-00-01-00'){throw 'Icon must be 256 x 256'}
 }finally{$icon.Dispose()}
 $readme=Read-EntryText 'README.md'
 foreach($shot in @('character','atlas','leaderboard')){
  $url="https://raw.githubusercontent.com/pendulumgames/ValheimSagas/main/docs/screenshots/$shot.png"
  if(!$readme.Contains($url)){throw "README missing public screenshot URL: $shot"}
 }
 if($readme -match '(?i)(C:\\Users\\|C:\\Repository\\|synthetic-development-token-only)'){throw 'Local path or development credential in public README'}
 $artSizes=@{}
 foreach($asset in ($webAssets | Where-Object {$_ -like '*.webp'})){
  $stream=$zip.GetEntry($prefix+'web/'+$asset).Open()
  try{$artSizes[$asset]=Get-WebPSize $stream}finally{$stream.Dispose()}
 }
 Assert-WebArtworkSizes $artSizes
 $lines=(Read-EntryText 'SHA256SUMS.txt').Split("`n")
 $inventoried=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($line in $lines){
  if(!$line.Trim()){continue}
  if($line.Trim() -cnotmatch '^([a-f0-9]{64})  (.+)$'){throw 'Malformed checksum entry'}
  $want=$Matches[1];$name=$Matches[2]
  if($name -eq 'SHA256SUMS.txt' -or !$inventoried.Add($name)){throw "Duplicate/self checksum entry: $name"}
  $entry=$zip.GetEntry($name);if(!$entry){throw "Missing inventoried file $name"}
  $sha=[Security.Cryptography.SHA256]::Create();$stream=$entry.Open()
  try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
  if($hash -cne $want){throw "Checksum mismatch $name"}
 }
 if($inventoried.Count -ne $actual.Count-1){throw 'Checksum inventory does not cover every release file'}
 Write-Host "PASS package: $($inventoried.Count) SHA-256 entries, four allowed DLLs, $($webAssets.Count) allowlisted web assets, exact public documentation inventory, 256px icon, manifest and screenshot links."
} finally {$zip.Dispose()}
