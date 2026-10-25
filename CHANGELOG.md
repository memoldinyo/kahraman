# Änderungen

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
