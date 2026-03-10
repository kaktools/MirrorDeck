# MirrorDeck

MirrorDeck ist eine WinUI-3-Steuerzentrale für Windows und führt AirPlay- und Android-Mirroring in einer App zusammen.

Aktuelle Version: `1.3.1`

## Highlights

- Einheitliche Steuerung für AirPlay und Android
- Dashboard mit gleichmäßig verteilten Aktionsflächen
- Status-Chips in den Kopfzeilen (u. a. Bonjour, UxPlay, scrcpy)
- Snapshot nur aus aktiven Mirroring-Fenstern (kein Desktop-Fallback)
- Globale Shortcuts (Snapshot und Pause/Fortsetzen)
- Start-/Exit-Overlay mit Fortschritt und Status-Texten
- Eigene Popup-Fenster für Log und Hilfe
- Einstellungen speichern sofort bei Änderung (kein Speichern-Button)

## Installation

### Setup-Installer

Installer-Datei aus `dist/` verwenden:

- `dist/MirrorDeck-Setup-1.3.0.exe`

### Portable Build

- `dist/portable/MirrorDeck/`

## Systemvoraussetzungen

- Windows 11 (empfohlen)
- .NET 8 Runtime/SDK (für lokale Entwicklung)
- Internetzugang für Download-Pfade von Abhängigkeiten

Build-Werkzeuge:

- Visual Studio 2022 mit Windows App SDK
- Inno Setup 6 (`ISCC.exe`) für den Installer
- Optional: MSIX-Paketierungs-/Signaturtools

## Build

Im Repository-Root:

```bat
build.bat portable
build.bat installer
build.bat msix
build.bat all
build.bat all --bump
```

Artefakte:

- Portable: `dist/portable/MirrorDeck/`
- Installer: `dist/MirrorDeck-Setup-<version>.exe`
- MSIX: `dist/msix/`

## Visueller QA-Lauf

Für Rendering-/DPI-Checks:

- Plan: `MirrorDeck.WinUI/SMOKE_TEST_RUNTIME_PLAN.md`
- Ergebnisvorlage: `artifacts/visual-qa/2026-03-08/results-template.md`

## Projektstruktur

- `MirrorDeck.WinUI/`: Hauptanwendung (Views, ViewModels, Services)
- `MirrorDeck.Bootstrapper/`: Bootstrapper
- `MirrorDeck.Package/`: MSIX-Projekt
- `script.iss`: Inno-Setup-Skript
- `scripts/`: Build-/Vendor-Skripte
- `vendor/`: vendorte Quellen/Artefakte

## Hinweise

- Der separate First-Start-Dialog ist weiterhin deaktiviert (Stabilitätsmodus).
- Modul-Aktivierung erfolgt direkt über Setup/Einstellungen.

## Troubleshooting

- Fehlende Tools: Setup-Assistent ausführen und Status prüfen.
- Download-Probleme: Firewall/Proxy/Internetverbindung prüfen.
- Android: `adb devices` und Debugging-Freigabe auf dem Gerät prüfen.
- AirPlay: Bonjour-Status und Netzwerkprofil prüfen.

## Lizenz

- Siehe `LICENSE.md` für Lizenztext und Third-Party-Hinweise.
