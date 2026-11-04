param([ValidateSet('lunch', 'overnight', 'break-late')][string]$Scenario = 'lunch', [string]$Language = 'de',
      [string]$Theme = 'Light', [switch]$Confirm, [string]$OutDir = "$(Split-Path $PSScriptRoot)\testdata\shots")
# Prüft die Rückfrage nach einer Abwesenheit mit einer Test-Instanz (eigene Einstellungen, Demodaten):
# legt einen Abwesenheits-Zustand an, simuliert eine Eingabe (Rückkehr), fotografiert das Fenster und
# klickt mit -Confirm auf "Eintragen". Danach stehen die letzten Logzeilen in der Ausgabe.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class AT {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct INPUT { public int type; public int dx, dy, mouseData, flags, time; public IntPtr extra; public int pad1, pad2; }
  [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
  public static void Nudge() { var i = new INPUT[1]; i[0].type = 0; i[0].flags = 1; SendInput(1, i, Marshal.SizeOf(typeof(INPUT))); }
}
"@
[AT]::SetProcessDPIAware() | Out-Null
$base = Split-Path $PSScriptRoot
$exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
$dir = "$base\testdata\tmp\away-test"
New-Item -ItemType Directory -Force $dir, $OutDir | Out-Null
& "$PSScriptRoot\demo-data.ps1" -Dir "$dir\data" | Out-Null
$log = "$dir\data\$env:USERNAME.kahraman"
$now = Get-Date
$fmt = 'yyyy-MM-dd HH:mm:ss'
switch ($Scenario) {
  'lunch'      { $lastAt = $now.AddMinutes(-80); $away = @{ Since = $now.AddMinutes(-50).ToString('s'); Kind = 'Lock' } }
  'overnight'  { $lastAt = $now.Date.AddDays(-1).AddHours(16).AddMinutes(30); $away = @{ Since = $now.Date.AddDays(-1).AddHours(17).AddMinutes(2).ToString('s'); Kind = 'Shutdown' } }
  'break-late' { $lastAt = $now.AddMinutes(-45); $away = @{ Since = $lastAt.ToString('s'); Kind = 'Break'; PlannedEnd = $now.AddMinutes(-15).ToString('s'); WorkLogged = $true } }
}
# Demodaten enden vor dem Testzeitpunkt; dazu ein letzter Eintrag passend zum Szenario.
$lines = [IO.File]::ReadAllLines($log, [Text.Encoding]::GetEncoding(1252)) | Where-Object { $_ -and $_.Substring(0, 19) -lt $lastAt.ToString($fmt) }
$lines += "$($lastAt.ToString($fmt))`tWebsite Relaunch`tDevelopment`t`"`""
[IO.File]::WriteAllText($log, ($lines -join "`r`n") + "`r`n", [Text.Encoding]::GetEncoding(1252))
$settings = "$dir\settings.json"
@{ DataDir = "$dir\data"; IntervalMinutes = 60; Language = $Language; Theme = $Theme; Away = $away } | ConvertTo-Json -Depth 3 | Set-Content $settings -Encoding utf8

$A = [Windows.Automation.AutomationElement]; $T = [Windows.Automation.TreeScope]
$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings)
try {
  $dlg = $null
  for ($i = 0; $i -lt 20 -and -not $dlg; $i++) {
    [AT]::Nudge(); Start-Sleep 1
    $dlg = $A::RootElement.FindAll($T::Children, (New-Object Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) |
      Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } | Select-Object -First 1
  }
  if (-not $dlg) { "Kein Fenster erschienen"; return }
  Start-Sleep 1
  $r = $dlg.Current.BoundingRectangle
  $b = New-Object Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
  $g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $b.Size); $g.Dispose()
  $file = "$OutDir\away-$Scenario-$Language-$Theme.png"; $b.Save($file); $b.Dispose()
  "Fenster: $($dlg.Current.Name) -> $file"
  $texts = $dlg.FindAll($T::Descendants, [Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name } | Where-Object { $_ }
  "Inhalt: " + ($texts -join ' | ')
  if ($Confirm) {
    $ok = $dlg.FindAll($T::Descendants, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, [Windows.Automation.ControlType]::Button))) |
      Where-Object { $_.Current.Name -match '^(Eintragen|Log it)$' } | Select-Object -First 1
    $ok.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep 2
    "Log danach:"; Get-Content $log -Tail 4 -Encoding 1252
    "Away danach: " + ((Get-Content $settings -Raw | ConvertFrom-Json).Away | ConvertTo-Json -Compress)
  }
}
finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
