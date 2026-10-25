param([string]$Theme = 'Dark', [string]$Language = 'de', [string]$OutDir = "$(Split-Path $PSScriptRoot)\testdata\shots")
# Öffnet das Kontextmenü der Tätigkeitenliste per Fensternachricht (ohne Mausklick) und fotografiert Abfrage samt Menü.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class MS { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l); }
"@
[MS]::SetProcessDPIAware() | Out-Null
$A = [System.Windows.Automation.AutomationElement]; $T = [System.Windows.Automation.TreeScope]
$base = Split-Path $PSScriptRoot; $settings = "$base\testdata\theme-settings.json"; $exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
Get-Process Kahraman -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\bin\Debug\*' } | Stop-Process -Force
@{ DataDir = "$base\testdata\demo"; IntervalMinutes = 60; Autostart = $false; Language = $Language; Theme = $Theme
   Windows = @{ 'InputForm.Popup' = @{ X = 200; Y = 150; Width = 720; Height = 330 } } } | ConvertTo-Json -Depth 4 | Set-Content $settings -Encoding utf8
$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings)
try {
  Start-Sleep 4
  $popup = $A::RootElement.FindAll($T::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } | Select-Object -First 1
  $list = $popup.FindAll($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::List))) | Select-Object -First 1
  $h = [IntPtr]$list.Current.NativeWindowHandle
  $lp = [IntPtr]((20 -shl 16) -bor 40)
  [MS]::PostMessage($h, 0x204, [IntPtr]2, $lp) | Out-Null   # WM_RBUTTONDOWN
  [MS]::PostMessage($h, 0x205, [IntPtr]0, $lp) | Out-Null   # WM_RBUTTONUP
  Start-Sleep 2
  $r = $popup.Current.BoundingRectangle
  $b = New-Object System.Drawing.Bitmap ([int]$r.Width + 120), ([int]$r.Height)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $b.Size)
  $g.Dispose(); $b.Save("$OutDir\menu-$Theme.png"); $b.Dispose()
  "menu $Theme fertig"
}
finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
Remove-Item $settings -ErrorAction SilentlyContinue
