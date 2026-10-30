# Prüft das Store-Paket auf diesem Rechner (Entwicklermodus nötig): registriert obj\msix\layout als Test-App
# mit eigenen Einstellungen und Demodaten, öffnet die Einstellungen im Paketkontext und sucht den Autostart-Link.
# -Remove entfernt die Test-App wieder.
param([switch]$Remove)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$name = (Get-Content "$root\packaging\msix\identity.json" -Raw | ConvertFrom-Json).IdentityName

Get-AppxPackage -Name $name | ForEach-Object {
    Get-Process Kahraman -ErrorAction SilentlyContinue | Where-Object Path -like "$($_.InstallLocation)*" | Stop-Process -Force
    Remove-AppxPackage $_.PackageFullName
}
if ($Remove) { "Test-App entfernt"; return }

$test = "$root\testdata\tmp\msix-test"
if (Test-Path $test) { Remove-Item $test -Recurse -Force }
Copy-Item "$root\obj\msix\layout" $test -Recurse
& "$root\testdata\demo-data.ps1" -Dir "$test\data" | Out-Null
$settings = "$test\settings.json"
@{ DataDir = "$test\data"; Language = "de" } | ConvertTo-Json | Set-Content $settings -Encoding utf8

# Testeinstellungen als Startparameter, damit das echte Kahraman unberührt bleibt.
$manifest = "$test\AppxManifest.xml"
$xml = [IO.File]::ReadAllText($manifest)
$xml = $xml.Replace('IgnorableNamespaces="uap desktop rescap"',
    'xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10" IgnorableNamespaces="uap desktop rescap uap10"').
    Replace('EntryPoint="Windows.FullTrustApplication">' + "`n" + '      <uap:VisualElements',
    "EntryPoint=`"Windows.FullTrustApplication`" uap10:Parameters=`"--settings &quot;$settings&quot;`">`n      <uap:VisualElements")
[IO.File]::WriteAllText($manifest, $xml)
Add-AppxPackage -Register $manifest
$pkg = Get-AppxPackage -Name $name
"Registriert: $($pkg.PackageFullName)"

Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!Kahraman"
Start-Sleep 4
$proc = Get-Process Kahraman -ErrorAction SilentlyContinue | Where-Object Path -like "$test*"
"Läuft aus dem Paket: $([bool]$proc) ($($proc.Path))"

Invoke-CommandInDesktopPackage -PackageFamilyName $pkg.PackageFamilyName -AppId Kahraman `
    -Command "$test\Kahraman.exe" -Args "--settings `"$settings`" --einstellungen"
Start-Sleep 3
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ae = [Windows.Automation.AutomationElement]
$win = $ae::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,
    (New-Object Windows.Automation.PropertyCondition($ae::ProcessIdProperty, $proc.Id)))
$texts = $win.FindAll([Windows.Automation.TreeScope]::Descendants, [Windows.Automation.Condition]::TrueCondition) |
    ForEach-Object { $_.Current.Name } | Where-Object { $_ -match 'Autostart' }
"Fenster: $($win.Current.Name)"
"Autostart-Elemente: $($texts -join ' | ')"
