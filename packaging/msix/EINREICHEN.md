# Kahraman im Microsoft Store einreichen

App: **Kahraman Time Tracker**, Store-ID `9N8VTGX1XRK1`, Herausgeber `MkMone`
(Identität in `identity.json`, aus Partner Center › Product management › Product identity).

## Paket bauen

```
./build-release.ps1
./build-msix.ps1
```

Ergebnis: `dist\Kahraman-<Version>-x64.msix` (unsigniert – der Store signiert es selbst).
Dasselbe Paket baut GitHub Actions bei jedem Push (Artefakt `Kahraman-<Version>-msix` im Lauf).
`testdata\msix-test.ps1` registriert das Paket zum Ausprobieren als Test-App (Entwicklermodus), `-Remove` entfernt sie.

## Einreichung im Partner Center

*Apps and games › Kahraman Time Tracker › Start submission*

1. **Pricing and availability**
   - Markets: alle; Visibility: *Public*; Pricing: *Free*.
2. **Properties**
   - Category: *Productivity* (Subcategory leer lassen bzw. passend wählen).
   - Privacy policy URL: `https://github.com/memoldinyo/kahraman#privacy`
   - Website: `https://github.com/memoldinyo/kahraman`, Support: `https://github.com/memoldinyo/kahraman/issues`
   - Product declarations: nichts besonders ankreuzen (kein Zugriff aufs Netzwerk, keine Konten).
3. **Age ratings** – Fragebogen: Kategorie *Productivity/Utility*, keine Nutzerinteraktion, keine Datenweitergabe,
   keine Käufe → ergibt meist „3+“ / „USK 0“.
4. **Packages** – `Kahraman-<Version>-x64.msix` hochladen. Device family: nur *Desktop*.
5. **Store listings** – je Sprache die Texte aus `STORE-LISTING.md` einfügen. Screenshots (1366×768) erzeugt
   `testdata/store-shots.ps1` in `dist\store\screenshots`: `1-ask-<Sprache>.png`, `2-dark-mode.png`, `3-languages.png`.
6. **Submission options › Restricted capabilities** – Begründung für `runFullTrust`:

   > Kahraman is a classic Win32 desktop application (Windows Forms, .NET). It needs runFullTrust to run as a
   > desktop app: it shows a notification-area (tray) icon, registers global hotkeys chosen by the user and reads and
   > writes its plain-text data files in a folder the user chooses (also on network drives).

   Notes for certification:

   > No account or test data needed. On first start the app asks what you are working on; pick or type any
   > activity and press Enter. Right-click the tray icon for the report, history and settings. The app has no network code.

7. **Submit to the Store.** Die Prüfung dauert meist ein bis drei Werktage.

## Neue Version

`<Version>` in `src/Kahraman/Kahraman.csproj` erhöhen (der Store verlangt eine höhere Version als die letzte),
Pakete bauen, im Partner Center *Update* → neue `.msix` hochladen, „What's new“ ausfüllen, einreichen.
