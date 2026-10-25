# Prüft: Die Abfrage lässt sich nicht schließen (Schließen-Befehl wie X/Alt+F4, Esc), Beenden über --beenden funktioniert aber.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class NC { [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetClassLong(IntPtr h, int i); }
"@
$A = [System.Windows.Automation.AutomationElement]; $T = [System.Windows.Automation.TreeScope]
$base = Split-Path $PSScriptRoot; $exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"; $settings = "$base\testdata\noclose-settings.json"
Get-Process Kahraman -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*\bin\Debug\*' } | Stop-Process -Force
@{ DataDir = "$base\testdata\demo"; IntervalMinutes = 60; Autostart = $false; Language = 'de'; Theme = 'Dark' } | ConvertTo-Json | Set-Content $settings -Encoding utf8
function Popup($p) { $A::RootElement.FindAll($T::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } | Select-Object -First 1 }
$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings); Start-Sleep 5
$popup = Popup $p; $h = [IntPtr]$popup.Current.NativeWindowHandle
"CS_NOCLOSE gesetzt: $(([NC]::GetClassLong($h, -26) -band 0x200) -ne 0)"
[NC]::PostMessage($h, 0x112, [IntPtr]0xF060, 0) | Out-Null; Start-Sleep 1   # WM_SYSCOMMAND SC_CLOSE = X, Alt+F4, Taskleiste
"nach Schließen-Befehl noch offen: $([bool](Popup $p))"
$edit = $popup.FindAll($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit))) | Select-Object -First 1
[NC]::PostMessage([IntPtr]$edit.Current.NativeWindowHandle, 0x100, [IntPtr]0x1B, [IntPtr]0x00010001) | Out-Null; Start-Sleep 1
"nach Esc noch offen: $([bool](Popup $p))"
Start-Process $exe -ArgumentList @('--settings', $settings, '--beenden')
"Beenden: Prozess beendet = $($p.WaitForExit(8000))"
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item $settings -ErrorAction SilentlyContinue
