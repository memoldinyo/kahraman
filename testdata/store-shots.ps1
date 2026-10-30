param([string]$Out = "$(Split-Path $PSScriptRoot)\dist\store\screenshots")
# Screenshots für den Microsoft Store (1366×768, Mindestgröße für Desktop) aus den Bildern in images/:
# 1 = Abfrage in der jeweiligen Sprache mit Auswertung und Werbezeile, 2 = Dunkelmodus, 3 = alle Sprachen.
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot
$img = "$root\images"
$W = 1366; $H = 768

function Canvas($top, $bottom) {
  $b = New-Object Drawing.Bitmap $W, $H
  $g = [Drawing.Graphics]::FromImage($b)
  $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'
  $brush = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.Rectangle 0, 0, $W, $H), $top, $bottom, 90
  $g.FillRectangle($brush, 0, 0, $W, $H); $brush.Dispose()
  , @($b, $g)
}

function Place($g, $file, $x, $y) {
  $i = [Drawing.Image]::FromFile($file)
  # weicher Schatten
  for ($s = 14; $s -ge 1; $s -= 1) {
    $shadow = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(6, 0, 0, 0))
    $g.FillRectangle($shadow, $x - $s + 4, $y - $s + 10, $i.Width + 2 * $s, $i.Height + 2 * $s); $shadow.Dispose()
  }
  $g.DrawImage($i, $x, $y, $i.Width, $i.Height); $i.Dispose()
}

function Headline($g, $text, $lang, $color) {
  $family = if ($lang -eq 'zh') { 'Microsoft YaHei UI' } else { 'Segoe UI Semibold' }
  $font = New-Object Drawing.Font $family, 30, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
  $fmt = New-Object Drawing.StringFormat
  $fmt.Alignment = 'Center'
  if ($lang -eq 'ar') { $fmt.FormatFlags = [Drawing.StringFormatFlags]::DirectionRightToLeft }
  $brush = New-Object Drawing.SolidBrush $color
  $g.DrawString($text, $font, $brush, (New-Object Drawing.RectangleF 40, 38, ($W - 80), 60), $fmt)
  $brush.Dispose(); $font.Dispose(); $fmt.Dispose()
}

$light1 = [Drawing.Color]::FromArgb(222, 236, 249); $light2 = [Drawing.Color]::FromArgb(246, 246, 246)
$dark1 = [Drawing.Color]::FromArgb(24, 40, 58); $dark2 = [Drawing.Color]::FromArgb(28, 28, 28)

# 2 und 3 sind für alle Sprachen gleich.
New-Item -ItemType Directory -Force $Out | Out-Null
$c = Canvas $dark1 $dark2; $b = $c[0]; $g = $c[1]
Place $g "$img\report-en-dark.png" 90 110
Place $g "$img\settings-en-dark.png" ($W - 606 - 90) 170
$g.Dispose(); $b.Save("$Out\2-dark-mode.png", [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

$c = Canvas $light1 $light2; $b = $c[0]; $g = $c[1]
$i = [Drawing.Image]::FromFile("$img\languages.png"); $x = [int](($W - $i.Width) / 2); $i.Dispose()
Place $g "$img\languages.png" $x 2
$g.Dispose(); $b.Save("$Out\3-languages.png", [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()

foreach ($lang in 'en', 'de', 'tr', 'fr', 'it', 'ar', 'zh', 'ru') {
  $ui = Get-Content "$root\src\Kahraman.Core\i18n\ui.$lang.json" -Raw -Encoding utf8 | ConvertFrom-Json
  $c = Canvas $light1 $light2; $b = $c[0]; $g = $c[1]
  Headline $g $ui.'app.tagline' $lang ([Drawing.Color]::FromArgb(27, 27, 27))
  Place $g "$img\report-en.png" 110 150
  Place $g "$img\popup-$lang.png" ($W - 686 - 110) ($H - 293 - 70)
  $g.Dispose(); $b.Save("$Out\1-ask-$lang.png", [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}
Get-ChildItem $Out | Select-Object Name, Length
