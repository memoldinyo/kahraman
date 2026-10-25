param([string]$Dir = "$(Split-Path $PSScriptRoot)\testdata\demo")
# Erzeugt erfundene Beispieldaten (keine echten Projekte oder Kunden) für Screenshots.
New-Item -ItemType Directory -Force $Dir | Out-Null
Get-ChildItem $Dir -Filter *.kahraman | Remove-Item

# Status: 0 Normal, 1 Allgemein (grün), 2 Projekt (rot hinterlegt), 3 Unwichtig (grau), 4 Wichtig (fett), 5 Standard (blau)
$activities = [ordered]@{
  'Accounting'            = @(1, @('Invoices', 'Travel expenses'))
  'Break'                 = @(3, @())
  'Customer Support'      = @(5, @('Hotline', 'E-mail', 'Remote session'))
  'Internal Meeting'      = @(1, @('Team meeting', 'One-on-one', 'All hands'))
  'Mobile App 2.0'        = @(2, @('Design', 'Development', 'Testing', 'Sprint review'))
  'Product Launch Q4'     = @(4, @('Press kit', 'Landing page', 'Webinar'))
  'Training'              = @(0, @('Online course', 'Workshop'))
  'Website Relaunch'      = @(2, @('Concept', 'Development', 'Content', 'SEO'))
  'Workshop Preparation'  = @(0, @('Slides', 'Exercises'))
}
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append("<?xml version=`"1.0`" encoding=`"ISO-8859-1`"?>`n")
[void]$sb.Append("<kahraman version=`"1`" lastchangetimestamp=`"$((Get-Date).ToString('dd.MM.yyyy HH:mm:ss'))`" lastchangeuser=`"demo`" lastchangehost=`"demo`">`n  <taetigkeiten>`n    <defaults>`n")
foreach ($d in 'Documentation', 'Phone call', 'Review') { [void]$sb.Append("      <taetigkeit text=`"$d`" status=`"0`"/>`n") }
[void]$sb.Append("    </defaults>`n")
foreach ($k in $activities.Keys) {
  $status, $subs = $activities[$k]
  if ($subs.Count -eq 0) { [void]$sb.Append("    <taetigkeit text=`"$k`" status=`"$status`"/>`n"); continue }
  [void]$sb.Append("    <taetigkeit text=`"$k`" status=`"$status`">`n      <taetigkeiten>`n")
  foreach ($s in $subs) { [void]$sb.Append("        <taetigkeit text=`"$s`" status=`"0`"/>`n") }
  [void]$sb.Append("      </taetigkeiten>`n    </taetigkeit>`n")
}
[void]$sb.Append("  </taetigkeiten>`n</kahraman>`n")
[System.IO.File]::WriteAllText("$Dir\taetigkeiten.kahraman", $sb.ToString().Replace("`r`n", "`n"), [System.Text.Encoding]::GetEncoding(28591))

# Einträge: Arbeitstage seit Monatsanfang, feste Zufallsfolge
$rnd = New-Object System.Random 42
$work = @('Website Relaunch', 'Mobile App 2.0', 'Customer Support', 'Product Launch Q4', 'Internal Meeting', 'Accounting', 'Training', 'Workshop Preparation')
$weights = @(8, 8, 5, 4, 4, 2, 1, 2)
function PickActivity { $t = ($weights | Measure-Object -Sum).Sum; $r = $rnd.Next($t); for ($i = 0; $i -lt $work.Count; $i++) { if ($r -lt $weights[$i]) { return $work[$i] }; $r -= $weights[$i] } }
$lines = New-Object System.Collections.Generic.List[string]
function Add($t, $a, $s, $c) { $lines.Add("$($t.ToString('yyyy-MM-dd HH:mm:ss'))`t$a`t$s`t`"$c`"") }
$today = (Get-Date).Date
$day = New-Object DateTime $today.Year, $today.Month, 1
while ($day -lt $today) {
  if ($day.DayOfWeek -ne 'Saturday' -and $day.DayOfWeek -ne 'Sunday') {
    $t = $day.AddHours(8).AddMinutes($rnd.Next(0, 40))
    Add $t 'Start' '' ''
    $lunch = $false
    while ($t.Hour -lt 17) {
      $t = $t.AddMinutes($rnd.Next(30, 110))
      if (-not $lunch -and $t.Hour -ge 12) { Add $t 'Break' '' 'Lunch'; $lunch = $true; continue }
      $a = PickActivity; $subs = $activities[$a][1]
      $s = if ($subs.Count) { $subs[$rnd.Next($subs.Count)] } else { '' }
      Add $t $a $s ''
    }
  }
  $day = $day.AddDays(1)
}
# Heute: ein paar Einträge, der letzte mit Kommentar (dann klappt das Kommentarfeld auf)
$now = Get-Date
Add $now.AddMinutes(-170) 'Start' '' ''
Add $now.AddMinutes(-110) 'Customer Support' 'E-mail' ''
Add $now.AddMinutes(-42) 'Mobile App 2.0' 'Sprint review' 'Demo for the product team'
[System.IO.File]::WriteAllText("$Dir\$env:USERNAME.kahraman", ($lines -join "`r`n") + "`r`n", [System.Text.Encoding]::GetEncoding(1252))
"Beispieldaten: $($lines.Count) Einträge in $Dir"
