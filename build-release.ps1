# Baut das Weitergabe-Paket: dist\Kahraman-<Version>-win-x64.zip
# Enthält eine eigenständige Kahraman.exe (inkl. .NET, keine Installation von .NET nötig), Anleitungen und Lizenz.
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

dotnet test tests/Kahraman.Core.Tests
if ($LASTEXITCODE -ne 0) { throw "Tests fehlgeschlagen" }

$version = ([xml](Get-Content src/Kahraman/Kahraman.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$out = "dist\Kahraman-$version"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish src/Kahraman -c Release -r win-x64 -o $out `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "Publish fehlgeschlagen" }

Copy-Item LIESMICH.txt, README.txt, LICENSE, NOTICE $out

# Lizenzhinweise der mitgelieferten .NET-Runtime (MIT) – Pflicht bei der Weitergabe.
$assets = Get-Content src/Kahraman/obj/project.assets.json -Raw
$packages = (dotnet nuget locals global-packages -l) -replace '^global-packages:\s*', ''
$notice = @("Kahraman enthält die .NET-Runtime und Windows Forms von Microsoft (.NET Foundation and Contributors).",
            "Diese Komponenten stehen unter der MIT-Lizenz; ihre Lizenz- und Drittanbieter-Hinweise folgen.", "")
foreach ($pack in "Microsoft.NETCore.App.Runtime.win-x64", "Microsoft.WindowsDesktop.App.Runtime.win-x64") {
    if ($assets -notmatch '"name": "' + [regex]::Escape($pack) + '",\s*"version": "\[([0-9][^,\]]*)') { throw "Runtime-Paket $pack nicht gefunden" }
    $packVersion = $Matches[1]
    $dir = Join-Path $packages "$($pack.ToLowerInvariant())\$packVersion"
    foreach ($file in Get-ChildItem $dir -File | Where-Object Name -match '^(LICENSE|THIRD-PARTY-NOTICES)') {
        $line = "=" * 78
        $notice += $line, "$pack $packVersion – $($file.Name)", $line, (Get-Content $file.FullName -Raw), ""
    }
}
$notice | Set-Content "$out\DRITTANBIETER-LIZENZEN.txt" -Encoding utf8

$zip = "dist\Kahraman-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out\Kahraman.exe", "$out\LICENSE", "$out\NOTICE", "$out\LIESMICH.txt", "$out\README.txt", "$out\DRITTANBIETER-LIZENZEN.txt" -DestinationPath $zip

Get-Item "$out\Kahraman.exe", $zip | Select-Object FullName, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
