# Prüft das Ein-/Ausklappen der Untertätigkeiten mit einer Kopie der Beispieldaten (letzter Eintrag ohne Untertätigkeit).
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class ST { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l); }
"@
[ST]::SetProcessDPIAware() | Out-Null
$A = [System.Windows.Automation.AutomationElement]; $T = [System.Windows.Automation.TreeScope]
$base = Split-Path $PSScriptRoot; $exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
$data = "$base\testdata\demo-subs"; $settings = "$base\testdata\subs-settings.json"; $out = "$base\testdata\shots"
Get-Process Kahraman -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\bin\Debug\*' } | Stop-Process -Force
if (Test-Path $data) { Remove-Item $data -Recurse -Force }
Copy-Item "$base\testdata\demo" $data -Recurse
$log = Get-ChildItem $data -Filter *.kahraman | Where-Object Name -ne 'taetigkeiten.kahraman' | Select-Object -First 1
Add-Content $log.FullName ("{0}`tBreak`t`t`"`"" -f (Get-Date).AddMinutes(-5).ToString('yyyy-MM-dd HH:mm:ss')) -Encoding Default
@{ DataDir = $data; IntervalMinutes = 60; Autostart = $false; Language = 'de'; Theme = 'Dark'
   Windows = @{ 'InputForm.Popup' = @{ X = 200; Y = 150; Width = 720; Height = 330 } } } | ConvertTo-Json -Depth 4 | Set-Content $settings -Encoding utf8

function Popup($p) { $A::RootElement.FindAll($T::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } | Select-Object -First 1 }
function Shot($el, $file) {
  $r = $el.Current.BoundingRectangle
  $b = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
  $g = [System.Drawing.Graphics]::FromImage($b); $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $b.Size); $g.Dispose(); $b.Save($file); $b.Dispose()
}
function Visible($popup) {
  ($popup.FindAll($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::List))) |
    Where-Object { -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 0 }).Count
}
function SubsSetting { (Get-Content $settings -Raw | ConvertFrom-Json).SubsExpanded }

# 1. Start: letzter Eintrag ohne Untertätigkeit -> eingeklappt
$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings); Start-Sleep 5
$popup = Popup $p
"1 Start: sichtbare Listen = $(Visible $popup) (erwartet 1), SubsExpanded = $(SubsSetting)"
Shot $popup "$out\subs-1-eingeklappt.png"

# 2. Leiste "Untertätigkeiten ausklappen" auslösen (wie ein Bildschirmleser) -> ausklappen, Zustand wird gespeichert
# Die beiden schmalen Leisten; die linke gehört zu den Untertätigkeiten.
$strip = $popup.FindAll($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Pane))) |
  Where-Object { $_.Current.BoundingRectangle.Width -gt 10 -and $_.Current.BoundingRectangle.Width -lt 40 } | Sort-Object { $_.Current.BoundingRectangle.X } | Select-Object -First 1
$sh = [IntPtr]$strip.Current.NativeWindowHandle   # Klick als Fensternachricht an die Leiste, der echte Mauszeiger bleibt, wo er ist
[ST]::PostMessage($sh, 0x201, [IntPtr]1, [IntPtr]((20 -shl 16) -bor 10)) | Out-Null   # WM_LBUTTONDOWN
[ST]::PostMessage($sh, 0x202, [IntPtr]0, [IntPtr]((20 -shl 16) -bor 10)) | Out-Null   # WM_LBUTTONUP
Start-Sleep 2
"2 Leiste: sichtbare Listen = $(Visible $popup) (erwartet 2), SubsExpanded = $(SubsSetting)"
Shot $popup "$out\subs-2-ausgeklappt.png"
Stop-Process -Id $p.Id -Force; Start-Sleep 1

# 3. Neustart: obwohl keine Untertätigkeit drinsteht, wieder ausgeklappt
$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings); Start-Sleep 5
$popup = Popup $p
"3 Neustart: sichtbare Listen = $(Visible $popup) (erwartet 2), SubsExpanded = $(SubsSetting)"
Shot $popup "$out\subs-3-neustart.png"
Stop-Process -Id $p.Id -Force
Remove-Item $settings, $data -Recurse -Force -ErrorAction SilentlyContinue
