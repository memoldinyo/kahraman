param([string[]]$Languages = @('en', 'de', 'tr', 'fr', 'it', 'ar', 'zh', 'ru'), [string]$OutDir = "$(Split-Path $PSScriptRoot)\images",
      [string]$Theme = 'Light', [string]$Suffix = '')
# Screenshots mit den Beispieldaten (testdata\demo) für die GitHub-Seite. Der echte Datenpfad wird übermalt.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class PS { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size); }
"@
[PS]::SetProcessDPIAware() | Out-Null
$base = Split-Path $PSScriptRoot
$exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
$demo = "$base\testdata\demo"
$settings = "$base\testdata\demo-settings.json"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$A = [System.Windows.Automation.AutomationElement]; $T = [System.Windows.Automation.TreeScope]; $all = [System.Windows.Automation.Condition]::TrueCondition
$fakePath = 'C:\Users\you\Documents\Kahraman\you.kahraman'

function Shot($el, $file) {
  # Sichtbarer Fensterrahmen ohne den unsichtbaren Größenänderungsrand (sonst ist ein Streifen Hintergrund mit drauf).
  $f = New-Object PS+RECT
  [PS]::DwmGetWindowAttribute([IntPtr]$el.Current.NativeWindowHandle, 9, [ref]$f, 16) | Out-Null   # DWMWA_EXTENDED_FRAME_BOUNDS
  $r = [System.Windows.Rect]::new($f.Left, $f.Top, $f.Right - $f.Left, $f.Bottom - $f.Top)
  $b = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
  $g = [System.Drawing.Graphics]::FromImage($b)
  $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $b.Size)
  # Texte mit dem echten Pfad übermalen (UIA meldet dasselbe Label oft mehrfach: erst alles füllen, dann einmal schreiben)
  $hits = @{}
  foreach ($t in $el.FindAll($T::Descendants, $all)) {
    $name = $t.Current.Name
    if ($name -and ($name -like "*$env:USERNAME*" -or $name -like '*testdata*')) {
      $tr = $t.Current.BoundingRectangle
      $rect = New-Object System.Drawing.Rectangle ([int]($tr.X - $r.X)), ([int]($tr.Y - $r.Y)), ([int]$tr.Width), ([int]$tr.Height)
      if (-not $hits.ContainsKey($name) -or $rect.Width * $rect.Height -lt $hits[$name].Width * $hits[$name].Height) { $hits[$name] = $rect }
    }
  }
  foreach ($name in $hits.Keys) {
    $rect = $hits[$name]
    $bg = $b.GetPixel([Math]::Max(0, $rect.X - 2), $rect.Y + 1)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $bg), $rect)
    $text = if ($name -like '*.kahraman') { $fakePath } else { Split-Path $fakePath }
    $dark = $Theme -eq 'Dark'
    $color = if ($name -like '*.kahraman') { if ($dark) { [System.Drawing.Color]::FromArgb(154, 154, 154) } else { [System.Drawing.Color]::FromArgb(107, 107, 107) } }
             else { if ($dark) { [System.Drawing.Color]::FromArgb(240, 240, 240) } else { [System.Drawing.Color]::FromArgb(27, 27, 27) } }
    $flags = [System.Windows.Forms.TextFormatFlags]::VerticalCenter -bor [System.Windows.Forms.TextFormatFlags]::NoPadding
    if ($lang -eq 'ar') { $flags = $flags -bor [System.Windows.Forms.TextFormatFlags]::Right }
    [System.Windows.Forms.TextRenderer]::DrawText($g, $text, [System.Drawing.SystemFonts]::MessageBoxFont, $rect, $color, $bg, $flags)
  }
  $g.Dispose(); $b.Save($file); $b.Dispose()
}
Add-Type -AssemblyName System.Windows.Forms
function Windows($p) { $A::RootElement.FindAll($T::Children, (New-Object System.Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) | Where-Object { $_.Current.BoundingRectangle.Width -gt 100 } }
function Signal($arg) { Start-Process $exe -ArgumentList @('--settings', $settings, $arg); Start-Sleep 2 }
function CloseWin($w) { [PS]::PostMessage([IntPtr]$w.Current.NativeWindowHandle, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; Start-Sleep 1 }

foreach ($lang in $Languages) {
  @{ DataDir = $demo; IntervalMinutes = 60; Autostart = $false; Language = $lang; Theme = $Theme
     Windows = @{ 'InputForm.Popup' = @{ X = 200; Y = 150; Width = 700; Height = 300 }; 'EvaluationForm' = @{ X = 200; Y = 100; Width = 720; Height = 560 } } } |
    ConvertTo-Json -Depth 4 | Set-Content $settings -Encoding utf8
  $p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings)
  try {
    Start-Sleep 4
    $popup = Windows $p | Select-Object -First 1
    Shot $popup "$OutDir\popup-$lang$Suffix.png"
    if ($lang -eq 'en') {
      CloseWin $popup
      Signal '--auswertung'
      $eval = Windows $p | Select-Object -First 1
      if ($eval) { Shot $eval "$OutDir\report-en$Suffix.png"; CloseWin $eval }
      Signal '--einstellungen'
      $dlg = Windows $p | Select-Object -First 1
      $items = $dlg.FindAll($T::Descendants, (New-Object System.Windows.Automation.PropertyCondition($A::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)))
      $items[0].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 400
      Shot $dlg "$OutDir\settings-en$Suffix.png"
      $items[3].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 400
      Shot $dlg "$OutDir\reminders-en$Suffix.png"
    }
    "$lang fertig"
  }
  finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Start-Sleep 1 }
}
Remove-Item $settings -ErrorAction SilentlyContinue
