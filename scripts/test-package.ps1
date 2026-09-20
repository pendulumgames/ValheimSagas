param([string]$Package='artifacts/ValheimSagas-0.3.10.zip')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(![IO.Path]::IsPathRooted($Package)){$Package=Join-Path $root $Package}
$verify=Join-Path $PSScriptRoot 'verify-package.ps1'
& $verify -Path $Package
Add-Type -AssemblyName System.IO.Compression.FileSystem
$work=Join-Path $root ('.dev/package-checks-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
function Set-ZipText($archive,$name,$text){
 $old=$archive.GetEntry($name);if($old){$old.Delete()}
 $entry=$archive.CreateEntry($name);$writer=[IO.StreamWriter]::new($entry.Open())
 try{$writer.Write($text)}finally{$writer.Dispose()}
}
function Get-ZipText($archive,$name){
 $reader=[IO.StreamReader]::new($archive.GetEntry($name).Open())
 try{return $reader.ReadToEnd()}finally{$reader.Dispose()}
}
$cases=[ordered]@{
 'unexpected-private-file'={param($z) Set-ZipText $z 'BepInEx/config/private.cfg' 'fixture'}
 'duplicate-file'={param($z) $e=$z.CreateEntry('README.md');$s=$e.Open();$s.Dispose()}
 'traversal-path'={param($z) Set-ZipText $z '../secret.txt' 'fixture'}
 'incomplete-inventory'={param($z) $lines=(Get-ZipText $z 'SHA256SUMS.txt').Split("`n");Set-ZipText $z 'SHA256SUMS.txt' ($lines[1..($lines.Length-1)] -join "`n")}
 'duplicate-inventory'={param($z) $text=Get-ZipText $z 'SHA256SUMS.txt';Set-ZipText $z 'SHA256SUMS.txt' ($text+"`n"+$text.Split("`n")[0])}
 'tampered-dll'={param($z) Set-ZipText $z 'BepInEx/plugins/ValheimSagas/Sagas.Core.dll' 'tampered synthetic package fixture'}
 'invalid-icon'={param($z) Set-ZipText $z 'icon.png' 'invalid png fixture'}
 'invalid-manifest'={param($z) Set-ZipText $z 'manifest.json' '{}'}
 'missing-public-screenshot'={param($z) Set-ZipText $z 'README.md' '# Synthetic negative test'}
}
foreach($case in $cases.GetEnumerator()){
 $dir=Join-Path $work $case.Key;New-Item -ItemType Directory -Path $dir | Out-Null
 $copy=Join-Path $dir ([IO.Path]::GetFileName($Package));Copy-Item -LiteralPath $Package -Destination $copy
 $z=[IO.Compression.ZipFile]::Open($copy,[IO.Compression.ZipArchiveMode]::Update)
 try{& $case.Value $z}finally{$z.Dispose()}
 $rejected=$false
 try{& $verify -Path $copy}catch{$rejected=$true;Write-Host "PASS rejected $($case.Key): $($_.Exception.Message)"}
 if(!$rejected){throw "Verifier accepted invalid package: $($case.Key)"}
}
Write-Host "PASS package validation: valid release accepted and $($cases.Count) malformed archives rejected. Isolated fixtures: $work"
