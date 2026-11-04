param([string]$Exe = (Get-ChildItem "$(Split-Path $PSScriptRoot)\dist\Kahraman-*\Kahraman.exe" | Sort-Object LastWriteTime | Select-Object -Last 1).FullName,
      [int]$Idle = 60)
# Misst Speicher und CPU einer Test-Instanz (eigene Einstellungen, Demodaten): nach dem Start, nach dem Öffnen der
# Fenster und im Leerlauf. Das echte Kahraman bleibt unberührt.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$dir = "$root\testdata\tmp\resource-test"
New-Item -ItemType Directory -Force $dir | Out-Null
& "$PSScriptRoot\demo-data.ps1" -Dir "$dir\data" | Out-Null
$settings = "$dir\settings.json"
@{ DataDir = "$dir\data"; Language = "de"; IntervalMinutes = 60 } | ConvertTo-Json | Set-Content $settings -Encoding utf8

function Show-Usage($p, $label) {
    $p.Refresh()
    "{0,-28} Working Set {1,6:N1} MB   privat {2,6:N1} MB   CPU {3,6:N2} s   Threads {4}" -f $label,
        ($p.WorkingSet64 / 1MB), ($p.PrivateMemorySize64 / 1MB), $p.TotalProcessorTime.TotalSeconds, $p.Threads.Count
}

$p = Start-Process $Exe -ArgumentList "--settings `"$settings`" --portable" -PassThru
Start-Sleep 8
Show-Usage $p "nach Start (Abfrage offen)"
foreach ($cmd in "--auswertung", "--einstellungen", "--logfile") {
    Start-Process $Exe -ArgumentList "--settings `"$settings`" $cmd" | Out-Null
    Start-Sleep 4
}
Show-Usage $p "Fenster geöffnet"
# Fenster schließen (Esc an alle Fenster der Instanz außer der Abfrage, die bleibt offen)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ae = [Windows.Automation.AutomationElement]
$isWindow = New-Object Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [Windows.Automation.ControlType]::Window)
$top = $ae::RootElement.FindAll([Windows.Automation.TreeScope]::Children,
    (New-Object Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $p.Id)))
foreach ($t in $top) {
    foreach ($w in @($t.FindAll([Windows.Automation.TreeScope]::Children, $isWindow)) + $t) {
        try { $w.GetCurrentPattern([Windows.Automation.WindowPattern]::Pattern).Close() } catch { }
    }
}
Start-Sleep 3
Show-Usage $p "Fenster geschlossen"
$c0 = $p.TotalProcessorTime
Start-Sleep $Idle
Show-Usage $p "nach $Idle s Leerlauf"
$p.Refresh(); "CPU im Leerlauf: {0:N3} s in $Idle s" -f ($p.TotalProcessorTime - $c0).TotalSeconds
Stop-Process $p -Force
