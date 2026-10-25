param([string[]]$Themes = @('Light', 'Dark'), [string]$Language = 'de', [string]$OutDir = "$(Split-Path $PSScriptRoot)\testdata\shots")
# Screenshots von Abfrage, Einstellungen und Auswertung im hellen und dunklen Modus (mit den Beispieldaten).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class TS { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l); }
"@
[TS]::SetProcessDPIAware() | Out-Null
$base = Split-Path $PSScriptRoot
$exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
$settings = "$base\testdata\theme-settings.json"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$A = [System.Windows.Automation.AutomationElement]; $T = [System.Windows.Automation.TreeScope]

function Shot($el, $file) {
  $r = $el.Current.BoundingRectangle
  $b = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $b.Size)
  $g.Dispose(); $b.Save($file); $b.Dispose()
}
function Windows($p) { $A::RootElement.FindAll($T::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } }
function Signal($arg) { Start-Process $exe -ArgumentList @('--settings', $settings, $arg); Start-Sleep 2 }
function CloseWin($w) { [TS]::PostMessage([IntPtr]$w.Current.NativeWindowHandle, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep 1 }

foreach ($theme in $Themes) {
  @{ DataDir = "$base\testdata\demo"; IntervalMinutes = 60; Autostart = $false; Language = $Language; Theme = $theme
     Windows = @{ 'InputForm.Popup' = @{ X = 200; Y = 150; Width = 720; Height = 330 }; 'EvaluationForm' = @{ X = 200; Y = 100; Width = 720; Height = 560 } } } |
    ConvertTo-Json -Depth 4 | Set-Content $settings -Encoding utf8
  $p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings)
  try {
    Start-Sleep 4
    $popup = Windows $p | Select-Object -First 1
    Shot $popup "$OutDir\popup-$theme.png"
    CloseWin $popup
    Signal '--einstellungen'
    $dlg = Windows $p | Select-Object -First 1
    Shot $dlg "$OutDir\settings-$theme.png"
    CloseWin $dlg
    Signal '--auswertung'
    $eval = Windows $p | Select-Object -First 1
    if ($eval) { Shot $eval "$OutDir\report-$theme.png"; CloseWin $eval }
    "$theme fertig"
  }
  finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Start-Sleep 1 }
}
Remove-Item $settings -ErrorAction SilentlyContinue
