param([string]$Language = 'de', [string]$Theme = 'Light', [string]$OutDir = "$(Split-Path $PSScriptRoot)\testdata\shots")
# Prüft die Menüpunkte "Pause", "Pause beenden", "Fragen pausieren" und "Feierabend" mit einer Test-Instanz:
# öffnet das Tray-Menü per Fensternachricht (ohne Mausklick), wählt die Punkte per UI Automation und zeigt Log und Zustand.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class PT {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public static List<IntPtr> Windows(uint pid) { var r = new List<IntPtr>(); EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) r.Add(h); return true; }, IntPtr.Zero); return r; }
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc f, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public static string Text(IntPtr h) { var s = new System.Text.StringBuilder(256); GetWindowText(h, s, 256); return s.ToString(); }
  public static IntPtr Child(IntPtr parent, string text) { IntPtr found = IntPtr.Zero; EnumChildWindows(parent, (h, l) => { if (Text(h).Replace("&", "") == text) { found = h; return false; } return true; }, IntPtr.Zero); return found; }
}
"@
[PT]::SetProcessDPIAware() | Out-Null
$A = [Windows.Automation.AutomationElement]; $T = [Windows.Automation.TreeScope]; $CT = [Windows.Automation.ControlType]
$base = Split-Path $PSScriptRoot
$exe = "$base\src\Kahraman\bin\Debug\net10.0-windows\Kahraman.exe"
$dir = "$base\testdata\tmp\pause-test"
& "$PSScriptRoot\demo-data.ps1" -Dir "$dir\data" | Out-Null
$log = "$dir\data\$env:USERNAME.kahraman"
$now = Get-Date
$lines = [IO.File]::ReadAllLines($log, [Text.Encoding]::GetEncoding(1252)) | Where-Object { $_ -and $_.Substring(0, 19) -lt $now.AddMinutes(-30).ToString('yyyy-MM-dd HH:mm:ss') }
[IO.File]::WriteAllText($log, ($lines -join "`r`n") + "`r`n", [Text.Encoding]::GetEncoding(1252))
$settings = "$dir\settings.json"
@{ DataDir = "$dir\data"; IntervalMinutes = 60; Language = $Language; Theme = $Theme } | ConvertTo-Json | Set-Content $settings -Encoding utf8

function Top($p) { $A::RootElement.FindAll($T::Children, (New-Object Windows.Automation.PropertyCondition($A::ProcessIdProperty, $p.Id))) }
function Named($root, $type, $pattern) {
  $root.FindAll($T::Descendants, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, $type))) | Where-Object { $_.Current.Name -match $pattern } | Select-Object -First 1
}
function Menu($p) {
  # NotifyIcon: Rechtsklick-Nachricht (WM_USER+1024, lParam = WM_RBUTTONUP) an alle Fenster des Prozesses.
  for ($try = 0; $try -lt 4; $try++) {
    foreach ($h in [PT]::Windows([uint32]$p.Id)) { [PT]::PostMessage($h, 0x800, [IntPtr]1, [IntPtr]0x205) | Out-Null }
    Start-Sleep 1
    foreach ($w in Top $p) {
      if ($w.FindFirst($T::Children, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, $CT::MenuItem)))) { return $w }
    }
  }
  "  Menü nicht gefunden (läuft: $(-not $p.HasExited)); Fenster: " + ((Top $p | ForEach-Object { "$($_.Current.ControlType.ProgrammaticName)/$($_.Current.ClassName)" }) -join ', ') | Write-Host
}
function Shot($el, $file) {
  $r = $el.Current.BoundingRectangle
  $b = New-Object Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
  $g = [Drawing.Graphics]::FromImage($b); $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $b.Size); $g.Dispose(); $b.Save($file); $b.Dispose()
}
function Click($el) { $el.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1.5 }
function AnswerPopup($p, $shot) {
  $popup = Top $p | Where-Object { $_.Current.BoundingRectangle.Width -gt 400 } | Select-Object -First 1
  if (-not $popup) { "  (keine Abfrage)"; return }
  $q = $popup.FindAll($T::Descendants, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, $CT::Text))) | Select-Object -First 1
  "  Frage: $($q.Current.Name)"
  if ($shot) { Shot $popup $shot }
  Start-Sleep 1.6   # Tastensperre nach dem Erscheinen
  $ok = $popup.FindAll($T::Descendants, (New-Object Windows.Automation.PropertyCondition($A::ControlTypeProperty, $CT::Button))) |
    Where-Object { $_.Current.BoundingRectangle.Width -gt 90 } | Select-Object -Last 1
  Click $ok
}
function State { $a = (Get-Content $settings -Raw | ConvertFrom-Json).Away; if ($a) { "  Zustand: $($a.Kind) seit $($a.Since) bis $($a.PlannedEnd)" } else { "  Zustand: –" } }
function Item($menu, $pattern) { Named $menu $CT::MenuItem $pattern }
function SubItem($parent, $pattern) {
  for ($try = 0; $try -lt 6; $try++) {
    try { $parent.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { }
    Start-Sleep 0.5
    $i = Named $parent $CT::MenuItem $pattern
    if ($i) { return $i }
  }
}

$p = Start-Process $exe -PassThru -ArgumentList @('--settings', $settings)
try {
  Start-Sleep 5
  "1. Start-Abfrage beantworten"; AnswerPopup $p $null; Start-Sleep 1

  "2. Menü › Pause › 30 Minuten"
  $m = Menu $p; Shot $m "$OutDir\pause-menu-$Language-$Theme.png"
  $brk = Item $m '^(&?P&?ause|Break|.*استراحة|休息.*|Перерыв)$'
  $brk.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep 1
  Click (SubItem $brk '^(30 Minuten|30 minutes|30 dakika|30 minuti|30 دقيقة|30 分钟|30 минут)$')
  AnswerPopup $p "$OutDir\pause-question-$Language-$Theme.png"; State

  "3. Menü › Pause beenden"
  $m = Menu $p; Shot $m "$OutDir\pause-menu-onbreak-$Language-$Theme.png"
  Click (Item $m 'beenden|End break|bitir|Termina|Terminer|إنهاء الاستراحة|结束休息|Закончить')
  State

  "4. Menü › Fragen pausieren › 1 Stunde"
  $m = Menu $p
  $pq = Item $m 'pausieren|Pause questions'
  $pq.GetCurrentPattern([Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep 1
  Click (SubItem $pq '^(1 Stunde|1 hour)$')
  $m = Menu $p; "  Menü zeigt: " + (($m.FindAll($T::Children, [Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name } | Where-Object { $_ }) -join ' | ')
  Click (Item $m 'fortsetzen|Resume questions')

  "5. Menü › Feierabend, danach › Feierabend aufheben"
  Start-Sleep 1
  $m = Menu $p; Click (Item $m 'Feier.?abend$|End of .?day')
  AnswerPopup $p $null; State
  # Die Rückfrage ist modal: UI Automation würde beim Klick warten, bis sie zu ist. Deshalb hier ohne UI Automation:
  # Menüpunkt in einem eigenen Prozess anklicken, Fenster per Titel suchen, "Eintragen" per BM_CLICK.
  $m = Menu $p
  $name = (Item $m 'aufheben|Back to work').Current.Name
  $job = Start-Job -ScriptBlock {
    param($h, $n)
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
    $e = [Windows.Automation.AutomationElement]::FromHandle([IntPtr]$h).FindFirst([Windows.Automation.TreeScope]::Descendants,
      (New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty, $n)))
    $e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
  } -ArgumentList $m.Current.NativeWindowHandle, $name
  $dlg = [IntPtr]::Zero
  for ($i = 0; $i -lt 10 -and $dlg -eq [IntPtr]::Zero; $i++) {
    Start-Sleep 0.5
    $dlg = [PT]::Windows([uint32]$p.Id) | Where-Object { [PT]::IsWindowVisible($_) -and [PT]::Text($_) -match 'Willkommen|Welcome' } | Select-Object -First 1
    if (-not $dlg) { $dlg = [IntPtr]::Zero }
  }
  if ($dlg -ne [IntPtr]::Zero) {
    "  Rückfrage: $([PT]::Text($dlg))"
    $r = New-Object PT+RECT; [PT]::GetWindowRect($dlg, [ref]$r) | Out-Null
    $b = New-Object Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T); $g = [Drawing.Graphics]::FromImage($b)
    $g.CopyFromScreen($r.L, $r.T, 0, 0, $b.Size); $g.Dispose(); $b.Save("$OutDir\pause-resume-$Language-$Theme.png"); $b.Dispose()
    [PT]::PostMessage([PT]::Child($dlg, $(if ($Language -eq 'de') { 'Eintragen' } else { 'Log it' })), 0xF5, [IntPtr]0, [IntPtr]0) | Out-Null   # BM_CLICK
    Start-Sleep 2
  } else { "  keine Rückfrage erschienen" }
  State
  "Log:"; Get-Content $log -Tail 6 -Encoding 1252
}
finally { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
