# Baut das Paket für den Microsoft Store: dist\Kahraman-<Version>-x64.msix
# Voraussetzung: build-release.ps1 ist gelaufen (dist\Kahraman-<Version>\Kahraman.exe).
# Das Paket bleibt unsigniert – der Store signiert es beim Veröffentlichen selbst.
param([string]$Identity = "$PSScriptRoot\packaging\msix\identity.json")
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$version = ([xml](Get-Content src/Kahraman/Kahraman.csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$app = "dist\Kahraman-$version"
if (-not (Test-Path "$app\Kahraman.exe")) { throw "$app\Kahraman.exe fehlt – zuerst build-release.ps1 ausführen" }

# makeappx und makepri: aus dem installierten Windows SDK, sonst aus dem NuGet-Paket von Microsoft.
function Find-SdkTool($name) {
    $sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.*\x64\$name" -ErrorAction SilentlyContinue |
        Sort-Object FullName | Select-Object -Last 1
    if ($sdk) { return $sdk.FullName }
    $toolsVersion = "10.0.28000.2957"
    $tools = "obj\sdk-buildtools\$toolsVersion"
    if (-not (Test-Path $tools)) {
        New-Item -ItemType Directory -Force $tools | Out-Null
        $nupkg = "$tools.zip"
        Invoke-WebRequest "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/$toolsVersion" -OutFile $nupkg
        Expand-Archive $nupkg $tools
        Remove-Item $nupkg
    }
    (Get-ChildItem "$tools\bin\*\x64\$name" | Select-Object -First 1).FullName
}
$makeappx = Find-SdkTool makeappx.exe
$makepri = Find-SdkTool makepri.exe

# Paketinhalt: Programm, Lizenzen, Symbole und Manifest mit Identität und Version (Store verlangt x.y.z.0).
$layout = "obj\msix\layout"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force "$layout\Assets" | Out-Null
Copy-Item "$app\Kahraman.exe", "$app\LICENSE", "$app\NOTICE", "$app\DRITTANBIETER-LIZENZEN.txt" $layout
Copy-Item packaging\msix\Assets\*.png "$layout\Assets"
$id = Get-Content $Identity -Raw | ConvertFrom-Json
$manifest = (Get-Content packaging\msix\AppxManifest.xml -Raw).
    Replace('$IdentityName$', $id.IdentityName).
    Replace('$Publisher$', [Security.SecurityElement]::Escape($id.Publisher)).
    Replace('$PublisherDisplayName$', [Security.SecurityElement]::Escape($id.PublisherDisplayName)).
    Replace('$DisplayName$', [Security.SecurityElement]::Escape($id.DisplayName)).
    Replace('$Version$', "$version.0")
[IO.File]::WriteAllText("$PWD\$layout\AppxManifest.xml", $manifest, (New-Object Text.UTF8Encoding $false))

# Ressourcenindex, damit Windows die passende Symbolgröße findet.
& $makepri createconfig /cf obj\msix\priconfig.xml /dq en-US /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig fehlgeschlagen" }
& $makepri new /pr $layout /cf obj\msix\priconfig.xml /mn "$layout\AppxManifest.xml" /of "$layout\resources.pri" /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri new fehlgeschlagen" }

$msix = "dist\Kahraman-$version-x64.msix"
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx pack fehlgeschlagen" }
Get-Item $msix | Select-Object FullName, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
