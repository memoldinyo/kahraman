param([string]$Theme = 'Dark', [string]$Sequence = 'popup,settings,eval')
# Prüft, ob Kahraman nach einer Folge von Fenstern noch reagiert (Testdaten, eigene Einstellungsdatei).
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class HT { [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l); }
"@
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$A = [System.Windows.Automation.AutomationElement]
function Wins($p) { $A::RootElement.FindAll([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } }
function CloseFirst($p) { $w = Wins $p | Select-Object -First 1; if ($w) { "  schließe [$($w.Current.Name)]"; [HT]::PostMessage([IntPtr]$w.Current.NativeWindowHandle, 0x10, 0, 0) | Out-Null }; Start-Sleep 1 }
$base = Split-Path $PSScriptRoot; $settings = "$base\testdata\theme-settings.json"; $exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
Get-Process Kahraman -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\bin\Debug\*' } | Stop-Process -Force
@{ DataDir = "$base\testdata\demo"; IntervalMinutes = 60; Autostart = $false; Language = 'de'; Theme = $Theme } | ConvertTo-Json | Set-Content $settings -Encoding utf8
$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings); Start-Sleep 4
foreach ($step in $Sequence.Split(',')) {
  switch ($step) {
    'popup' { CloseFirst $p }
    'settings' { Start-Process $exe -ArgumentList @('--settings', $settings, '--einstellungen'); Start-Sleep 2; CloseFirst $p }
    'eval' { Start-Process $exe -ArgumentList @('--settings', $settings, '--auswertung'); Start-Sleep 3 }
    'evalclose' { Start-Process $exe -ArgumentList @('--settings', $settings, '--auswertung'); Start-Sleep 3; CloseFirst $p }
  }
  Start-Sleep 3; $p.Refresh(); "$step -> responding=$($p.Responding)"
}
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
