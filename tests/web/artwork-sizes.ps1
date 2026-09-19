# Synthetic container headers exercise the package guard; no artwork or game files are read.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../../scripts/web-artwork.ps1')
$script:checked=0
function Check($Condition,$Message){if(!$Condition){throw $Message};$script:checked++}
function Header([string]$Chunk){$bytes=New-Object byte[] 30;[Text.Encoding]::ASCII.GetBytes('RIFF').CopyTo($bytes,0);[Text.Encoding]::ASCII.GetBytes('WEBP').CopyTo($bytes,8);[Text.Encoding]::ASCII.GetBytes($Chunk).CopyTo($bytes,12);return ,$bytes}
function Read-Size([byte[]]$Bytes){$stream=[IO.MemoryStream]::new($Bytes,$false);try{Get-WebPSize $stream}finally{$stream.Dispose()}}
function Reject($Action,$Message){$rejected=$false;try{& $Action | Out-Null}catch{$rejected=$true};Check $rejected $Message}
$bytes=Header 'VP8X';$bytes[24]=255;$bytes[25]=14;$bytes[27]=111;$bytes[28]=8
$size=Read-Size $bytes
Check ($size.Width -eq 3840 -and $size.Height -eq 2160) 'Extended WebP dimensions'
$bytes=Header 'VP8 ';$bytes[23]=157;$bytes[24]=1;$bytes[25]=42;$bytes[26]=128;$bytes[27]=7;$bytes[28]=56;$bytes[29]=4
$size=Read-Size $bytes
Check ($size.Width -eq 1920 -and $size.Height -eq 1080) 'Lossy WebP dimensions'
$bytes=Header 'VP8L';$bytes[20]=47;$bytes[21]=127;$bytes[22]=199;$bytes[23]=13;$bytes[24]=1
$size=Read-Size $bytes
Check ($size.Width -eq 1920 -and $size.Height -eq 1080) 'Lossless WebP dimensions'
Reject {Read-Size (New-Object byte[] 30)} 'Reject non-WebP data'
Reject {Read-Size ([byte[]]@(1,2,3))} 'Reject truncated data'
Reject {Read-Size (Header 'JUNK')} 'Reject unsupported chunk'
$sizes=@{}
foreach($composition in @('grounded')){foreach($biome in @('meadows','black-forest','swamp','mountain','plains','mistlands','ashlands','deep-north')){foreach($width in @(1920,2560,3840)){$sizes["biomes/$composition/$biome-$width.webp"]=[pscustomobject]@{Width=$width;Height=$width*9/16}}}}
Assert-WebArtworkSizes $sizes
Check ($sizes.Count -eq 24) 'Complete trio inventory'
$sizes['biomes/grounded/meadows-1920.webp'].Width=1536
Reject {Assert-WebArtworkSizes $sizes} 'Reject undersized image behind a higher-resolution filename'
$sizes['biomes/grounded/meadows-1920.webp'].Width=1920
$sizes['biomes/grounded/meadows-1920.webp'].Height=1280
Reject {Assert-WebArtworkSizes $sizes} 'Reject different variant composition aspect ratios'
$sizes.Remove('biomes/grounded/meadows-1920.webp')
Reject {Assert-WebArtworkSizes $sizes} 'Reject missing variant'
Write-Host "PASS $script:checked synthetic WebP package-guard assertions."
