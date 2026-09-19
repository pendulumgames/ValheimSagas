# Read only WebP container headers. No game assets or image libraries are required.
function Get-WebPSize([IO.Stream]$Stream) {
 $header=New-Object byte[] 30
 $read=0
 while($read -lt $header.Length){$count=$Stream.Read($header,$read,$header.Length-$read);if(!$count){break};$read+=$count}
 if($read -lt 25 -or [Text.Encoding]::ASCII.GetString($header,0,4) -ne 'RIFF' -or [Text.Encoding]::ASCII.GetString($header,8,4) -ne 'WEBP'){throw 'Invalid WebP container'}
 $chunk=[Text.Encoding]::ASCII.GetString($header,12,4)
 switch($chunk){
  'VP8X' {if($read -lt 30){throw 'Truncated WebP extended header'};$width=1+[int]$header[24]+([int]$header[25] -shl 8)+([int]$header[26] -shl 16);$height=1+[int]$header[27]+([int]$header[28] -shl 8)+([int]$header[29] -shl 16)}
  'VP8 ' {if($read -lt 30 -or $header[23] -ne 157 -or $header[24] -ne 1 -or $header[25] -ne 42){throw 'Invalid WebP VP8 frame header'};$width=([int]$header[26]+([int]$header[27] -shl 8)) -band 16383;$height=([int]$header[28]+([int]$header[29] -shl 8)) -band 16383}
  'VP8L' {if($header[20] -ne 47){throw 'Invalid WebP lossless header'};$width=1+([int]$header[21]+(([int]$header[22] -band 63) -shl 8));$height=1+(([int]$header[22] -shr 6)+([int]$header[23] -shl 2)+(([int]$header[24] -band 15) -shl 10))}
  default {throw "Unsupported WebP header $chunk"}
 }
 if($width -lt 1 -or $height -lt 1){throw 'Invalid WebP dimensions'}
 [pscustomobject]@{Width=$width;Height=$height}
}
function Assert-WebArtworkSizes($Sizes) {
 foreach($composition in @('grounded')){foreach($biome in @('meadows','black-forest','swamp','mountain','plains','mistlands','ashlands','deep-north')){
  $ratio=$null
  foreach($width in @(1920,2560,3840)){
   $asset="biomes/$composition/$biome-$width.webp"
   if(!$Sizes.ContainsKey($asset)){throw "Missing responsive artwork: $asset"}
   $size=$Sizes[$asset]
   if($size.Width -ne $width -or $size.Height -gt 3840){throw "Unexpected artwork dimensions: $asset is $($size.Width)x$($size.Height)"}
   $nextRatio=$size.Width/[double]$size.Height
   if($null -ne $ratio -and [Math]::Abs($ratio-$nextRatio) -gt 0.002){throw "Responsive artwork variants have different aspect ratios: $asset"}
   $ratio=$nextRatio
  }
 }}
 Write-Host 'PASS responsive artwork: 24 WebP files; 1920/2560/3840 widths and matching aspect ratios.'
}
