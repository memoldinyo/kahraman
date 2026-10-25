param(
  [string]$Template = "$(Split-Path $PSScriptRoot)\src\Kahraman\IconVorlage.png",
  [string]$Ico = "$(Split-Path $PSScriptRoot)\src\Kahraman\kahraman.ico",
  [string]$Png = "$(Split-Path $PSScriptRoot)\images\icon.png")
# Programmsymbol aus der Vorlage: grauen Hintergrund außerhalb des Zifferblatts durchsichtig machen,
# quadratisch zuschneiden und als .ico (PNG-komprimiert, mehrere Größen) und icon.png (256) speichern.
Add-Type -AssemblyName System.Drawing
$src = [System.Drawing.Image]::FromFile($Template)
$bmp = New-Object System.Drawing.Bitmap $src
$src.Dispose()
$w = $bmp.Width; $h = $bmp.Height
$rim = [System.Drawing.Color]::FromArgb(125, 180, 230)
$bg = $bmp.GetPixel(0, 0).R

# Flutfüllung von den Ecken: alles, was heller ist als der Rand, gehört zum Hintergrund.
# Kantenpixel (Mischung aus Rand und Grau) bekommen Randfarbe mit passender Deckkraft.
$seen = New-Object 'bool[,]' $w, $h
$stack = New-Object System.Collections.Generic.Stack[int]
foreach ($p in @(@(0, 0), @(($w - 1), 0), @(0, ($h - 1)), @(($w - 1), ($h - 1)))) { $stack.Push($p[1] * $w + $p[0]) }
while ($stack.Count) {
  $i = $stack.Pop(); $x = $i % $w; $y = [int][Math]::Floor($i / $w)
  if ($seen[$x, $y]) { continue }
  $seen[$x, $y] = $true
  $c = $bmp.GetPixel($x, $y)
  if ($c.R -lt $rim.R + 15) { continue }
  $a = [Math]::Max(0, [Math]::Min(255, [int](255 * ($bg - $c.R) / ($bg - $rim.R))))
  $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $rim.R, $rim.G, $rim.B))
  if ($x -gt 0) { $stack.Push($i - 1) }; if ($x -lt $w - 1) { $stack.Push($i + 1) }
  if ($y -gt 0) { $stack.Push($i - $w) }; if ($y -lt $h - 1) { $stack.Push($i + $w) }
}

# Sichtbaren Bereich bestimmen und quadratisch mit etwas Rand zuschneiden.
$minX = $w; $minY = $h; $maxX = 0; $maxY = 0
for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) {
  if ($bmp.GetPixel($x, $y).A -gt 16) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } }
} }
$side = [Math]::Max($maxX - $minX, $maxY - $minY) + 1
$side = [int]($side * 1.04)
$square = New-Object System.Drawing.Bitmap $side, $side
$g = [System.Drawing.Graphics]::FromImage($square)
$g.Clear([System.Drawing.Color]::Transparent)
$g.DrawImage($bmp, [int](($side - ($maxX - $minX + 1)) / 2) - $minX, [int](($side - ($maxY - $minY + 1)) / 2) - $minY)
$g.Dispose(); $bmp.Dispose()

function Scaled($size) {
  $b = New-Object System.Drawing.Bitmap $size, $size
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.DrawImage($square, 0, 0, $size, $size)
  $g.Dispose(); $b
}

$sizes = 16, 24, 32, 48, 64, 256
$images = foreach ($s in $sizes) { $b = Scaled $s; $ms = New-Object System.IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $b.Dispose(); , $ms.ToArray() }
$fs = [System.IO.File]::Create($Ico); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $d = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
  $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]$images[$i].Length); $bw.Write([int]$offset)
  $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Dispose()
$big = Scaled 256; $big.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png); $big.Dispose()
$square.Dispose()
"Icon: $Ico ($($sizes -join ', ') px), PNG: $Png"
