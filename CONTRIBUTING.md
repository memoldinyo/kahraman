# Contributing to Kahraman

Thanks for your interest! Bug reports, ideas, translations and pull requests are all welcome –
in any of the eight languages Kahraman speaks.

## Getting started

Requirements: Windows 10/11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build Kahraman.slnx
dotnet test tests/Kahraman.Core.Tests
```

Run the app from `src/Kahraman/bin/Debug/net10.0-windows/Kahraman.exe`. To try things without touching your
real data, start it with its own settings file – it then runs as a separate instance:

```
Kahraman.exe --settings C:\temp\test-settings.json --portable
```

`testdata/demo-data.ps1` creates fictional demo data in `testdata/demo` that you can point the data folder to.

## Project layout

| Path | Content |
|---|---|
| `src/Kahraman.Core` | file formats, evaluation, quips and all texts (`i18n/`) – no UI, covered by tests |
| `src/Kahraman` | Windows Forms UI, tray icon, installer |
| `src/Kahraman/Ui/Theme.cs` | light/dark mode, colours, menu renderer |
| `src/Kahraman/Ui/ModernControls.cs` | Windows-11-style buttons, check boxes, field frames, tabs, segmented switch |
| `tests/Kahraman.Core.Tests` | xUnit tests (file formats, evaluation, every language) |
| `testdata/` | helper scripts: demo data, screenshots, UI checks (use their own settings files and demo data only) |
| `images/` | pictures for this README |
| `build-release.ps1` | builds `dist/Kahraman-<version>-win-x64.zip` (self-contained single EXE) |

Code comments are mostly German; English comments are just as welcome.

## Translations

Every language has three files in `src/Kahraman.Core/i18n/`: `ui.<code>.json` (interface texts),
`quips.<code>.json` (the cheeky questions and fun facts – adapt them, don't just translate) and `help.<code>.txt`.

- Keys must match the German file (`ui.de.json`); placeholders like `{0}`, `$YES$`/`$NO$`/`$CANCEL$`
  and `&` mnemonics must stay consistent. `LanguageTests` checks all of this.
- A new language also needs an entry in `L.All` (`src/Kahraman.Core/L.cs`).
- The `i18n` files are embedded with `WithCulture="false"` – otherwise MSBuild treats `ui.de.json` as a satellite resource.

## Things worth knowing

- **Data format:** `<username>.kahraman` holds one line per entry
  (`yyyy-MM-dd hh:mm:ss<TAB>activity<TAB>sub-activity<TAB>"comment"`, CRLF, Windows-1252; lines with other characters as UTF-8).
  `taetigkeiten.kahraman` is XML with activities, sub-activities, templates and colour status (0–5).
  Files are written byte-exact and backed up as `*.bak` before every overwrite – please keep it that way.
- **Time accounting:** an entry counts for the time since the previous entry of the same day; the first entry of a day only marks the start.
- **Dark mode** is switched on in `TrayApp.Initialize`, i.e. once the message loop runs. Switching it on before
  `Application.Run` made the report window hang later.
- **The question window** can't be closed by the user on purpose (CS_NOCLOSE, `OnFormClosing`); it closes with an entry
  or when Kahraman quits.
- **Command line:** `--logfile`, `--auswertung` (report), `--einstellungen` (settings), `--beenden` (quit),
  `--portable`, `--uninstall`, `--settings <file.json>`.
- **App icon:** edit `src/Kahraman/IconVorlage.png`, then run `testdata/make-icon.ps1` (writes `kahraman.ico` and `images/icon.png`).
- **Tests and TEMP:** if file tests fail with "handle is invalid" in a sandboxed shell, point `TEMP`/`TMP` to a writable folder.

## Pull requests

1. Fork, create a branch, keep changes focused.
2. `dotnet build` without warnings and `dotnet test` green.
3. UI changes: please check light and dark mode, and Arabic (right-to-left) if layout is affected.
4. Describe what and why in the PR; screenshots help.

GitHub Actions (`.github/workflows/build.yml`) builds and tests every pull request.
Pull requests from people outside the team are reviewed by a maintainer before they are merged
(see the code signing policy in the [README](README.md#code-signing-policy)).

Changes are listed in [CHANGELOG.md](CHANGELOG.md).

## Releases

1. Raise `<Version>` in `src/Kahraman/Kahraman.csproj` and add a section to `CHANGELOG.md`.
2. Commit, then tag and push: `git tag v2.1.1` and `git push origin main v2.1.1`.
3. GitHub Actions builds, tests and attaches `Kahraman-<version>-win-x64.zip` to a **draft** release.
   Write the release notes and publish it. Only packages built there are published (and, once granted, signed via SignPath).

## License

Kahraman is licensed under the [Apache License 2.0](LICENSE). By contributing you agree that your contributions
are licensed under the same terms.
