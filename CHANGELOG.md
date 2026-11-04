# Änderungen

## 2.2.0 – 04.11.2026

### Neu
- **Pause und Feierabend im Menü** (Rechtsklick auf das Zifferblatt):
  - *Fragen pausieren* (30 Min., 1 Std., 2 Std., bis „Fragen fortsetzen“): keine Abfragen, die Zeit zählt weiter –
    z. B. bei Besprechungen oder Präsentationen.
  - *Pause* (gleiche Dauern): trägt zuerst die Arbeit bis jetzt ein, die Pause wird als „Pause“ gebucht. Wer später
    als geplant zurückkommt, wird gefragt: bis zum geplanten Ende oder bis jetzt? *Pause beenden* bucht bis jetzt.
  - *Feierabend*: trägt den letzten Stand ein und fragt bis zum nächsten Tag nicht mehr; *Feierabend aufheben*
    für den Fall, dass man doch noch einmal arbeitet.
  - Solange pausiert ist, ist das Zifferblatt grau; der Tooltip zeigt z. B. „Pause bis 12:30“.
- **Abwesenheit wird bemerkt:** Sperren (Win+L), Abmelden, Herunterfahren/Neustart, Energiesparmodus/Ruhezustand
  und Inaktivität (keine Maus/Tastatur). Bei der Rückkehr fragt Kahraman, woran man bis zum Gehen gearbeitet hat
  (Uhrzeit und Tätigkeit vorausgefüllt) und ob die Zeit dazwischen eine Pause war; am nächsten Tag wird der
  Arbeitsbeginn eingetragen. Während man weg ist, kommen keine Abfragen. Der Zustand übersteht ein Herunterfahren.
- **Einstellungen › Abwesenheit:** jede Erkennung einzeln ein-/ausschaltbar, Minuten bis zur Inaktivität (5–240),
  Mindestdauer für eine Rückfrage (1–120 Minuten), Name der Tätigkeit für Pausen.

### Geändert
- Die Abfrage schlägt nach einer Pause oder einem Arbeitsbeginn die letzte Arbeitstätigkeit vor, nicht „Pause“ bzw. „Start“.
- Hilfe in allen 8 Sprachen um „Pause, Feierabend und Abwesenheit“ ergänzt.
- Tests: `AwayTests` (Buchungslogik); `testdata/away-test.ps1` und `testdata/pause-test.ps1` prüfen Rückfrage und
  Menü mit einer Testinstanz.

## 2.1.2 – 04.11.2026

### Geändert
- **Weniger Arbeitsspeicher:** Die EXE wird nicht mehr komprimiert gebaut – komprimiert musste .NET sie beim Start
  ganz in den Speicher entpacken. Ein laufendes Kahraman braucht dadurch rund 20 statt 70 MB privaten Speicher;
  der Download bleibt fast gleich groß (die EXE selbst belegt 111 statt 50 MB auf der Festplatte).
- Keine Hintergrund-Speicherbereinigung und keine Laufzeit-Leistungsmessung (PGO) mehr: weniger Threads und
  CPU im Leerlauf. `testdata/resource-test.ps1` misst Speicher und CPU einer Testinstanz.

## 2.1.1 – 30.10.2026

### Neu
- **Microsoft Store:** Kahraman gibt es als Paket „Kahraman Time Tracker“ (von Microsoft signiert, aktualisiert sich
  selbst). `build-msix.ps1` baut es, `packaging/msix/` enthält Manifest, Symbole, Store-Texte in 8 Sprachen und die
  Anleitung zum Einreichen. Als Store-Paket überlässt Kahraman Installation, Updates und Autostart Windows;
  in den Einstellungen führt ein Link zu *Windows-Einstellungen › Apps › Autostart*.

### Projekt
- Automatischer Build mit GitHub Actions (`.github/workflows/build.yml`): Tests, ZIP- und Store-Paket bei jedem
  Push und Pull Request; ein Tag `vX.Y.Z` legt einen Release-Entwurf mit dem ZIP-Paket an.
- README: Abschnitte *Privacy* und *Code signing policy*; CONTRIBUTING: Ablauf für Releases.

## 2.1.0 – 25.10.2026

### Open Source
- Kahraman ist jetzt Open Source unter der **Apache-Lizenz 2.0** (`LICENSE`, `NOTICE`); die bisherige
  Freeware-Lizenz (LIZENZ.txt/LICENSE.txt) entfällt. Quellcode, Projektseite und Releases liegen gemeinsam unter
  <https://github.com/memoldinyo/kahraman>. Beiträge: siehe [CONTRIBUTING.md](CONTRIBUTING.md).
- Testdaten und Beispieltexte enthalten nur erfundene Namen; die Hilfsskripte in `testdata/` arbeiten mit Pfaden
  relativ zum Projekt.

### Neu
- **Hell- und Dunkelmodus:** Einstellungen › Allgemein › Darstellung mit *System / Hell / Dunkel*.
  „System“ folgt der Windows-Einstellung, auch wenn sie sich ändert (gilt für danach geöffnete Fenster).
- **Moderne Optik (Windows 11):** runde Rahmen um Listen und Eingabefelder mit Akzentlinie beim aktiven Feld,
  flache Knöpfe (Hauptaktion hellblau), runde Häkchen und Optionsfelder, Reiter mit Unterstreichung,
  Menüs mit runden Ecken, Schrift *Segoe UI Variable*. Farben passend zum Programmsymbol (Hellblau, Bordeaux).
- **Untertätigkeiten einklappbar:** eigene Leiste rechts neben den Untertätigkeiten oder Alt+U.
  Eingeklappt erscheinen sie nur, wenn für die Tätigkeit eine Untertätigkeit vorgeschlagen wird;
  der zuletzt von Hand gewählte Zustand wird gespeichert (`SubsExpanded` in `settings.json`).
- **Neues Programmsymbol** aus `src/Kahraman/IconVorlage.png` (Stundenzeiger auf 6, Minutenzeiger auf 1).

### Geändert
- Die Abfrage lässt sich nicht mehr schließen: X ausgegraut, Esc und Alt+F4 gesperrt. Sie verschwindet erst
  mit einem Eintrag (oder beim Beenden von Kahraman bzw. Abmelden von Windows).
- Statusfarben der Listen: Bordeaux statt Rot (Projekt, Wichtig), mit eigenen Varianten für den Dunkelmodus.
- Hilfe, LIESMICH und README in allen 8 Sprachen ergänzt.

### Behoben
- Auswertung konnte hängen bleiben, wenn der Dunkelmodus vor dem Programmstart eingeschaltet wurde
  (Modus wird jetzt erst in der laufenden Nachrichtenschleife gesetzt).

## 2.0.0 – 24.10.2026

Erste öffentliche Version (Programm unter <https://github.com/memoldinyo/kahraman>).
- Abfrage mit Tätigkeit, Untertätigkeit und Kommentar; einklappbares Kommentarfeld (Alt+K).
- Abfrage bleibt im Vordergrund; optional Blinken, Rütteln, Minimieren.
- Auswertung, Einträge bearbeiten, Importe, Vorlagen, Tastenkürzel.
- 8 Sprachen (Deutsch, Englisch, Türkisch, Französisch, Italienisch, Arabisch, Chinesisch, Russisch).
- Installation ohne Administratorrechte, eigenständige EXE.
