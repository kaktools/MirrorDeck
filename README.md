# MirrorDeck

MirrorDeck ist eine WinUI-3-Steuerzentrale für Windows und führt AirPlay- und Android-Mirroring in einer App zusammen.

Aktuelle Version: `1.3.3`

## Highlights

- Einheitliche Steuerung für AirPlay und Android
- Dashboard mit gleichmäßig verteilten Aktionsflächen
- Status-Chips in den Kopfzeilen (u. a. Bonjour, UxPlay, scrcpy)
- Snapshot nur aus aktiven Mirroring-Fenstern (kein Desktop-Fallback)
- Globale Shortcuts (Snapshot und Pause/Fortsetzen)
- Start-/Exit-Overlay mit Fortschritt und Status-Texten
- Eigene Popup-Fenster für Log und Hilfe
- Einstellungen speichern sofort bei Änderung (kein Speichern-Button)

## Systemvoraussetzungen

- Windows 11 (empfohlen)
- .NET 8 Runtime/SDK (für lokale Entwicklung)
- Internetzugang für Download-Pfade von Abhängigkeiten

Build-Werkzeuge:

- Visual Studio 2022 mit Windows App SDK
- Inno Setup 6 (`ISCC.exe`) für den Installer
- Optional: MSIX-Paketierungs-/Signaturtools


Artefakte:

- Portable: `dist/portable/MirrorDeck/`
- Installer: `dist/MirrorDeck-Setup-<version>.exe`
- MSIX: `dist/msix/`

## Visueller QA-Lauf

Für Rendering-/DPI-Checks:

- Plan: `MirrorDeck.WinUI/SMOKE_TEST_RUNTIME_PLAN.md`
- Ergebnisvorlage: `artifacts/visual-qa/2026-03-08/results-template.md`

## Hinweise

- Der separate First-Start-Dialog ist weiterhin deaktiviert (Stabilitätsmodus).
- Modul-Aktivierung erfolgt direkt über Setup/Einstellungen.

## Troubleshooting

- Fehlende Tools: Setup-Assistent ausführen und Status prüfen.
- Download-Probleme: Firewall/Proxy/Internetverbindung prüfen.
- Android: `adb devices` und Debugging-Freigabe auf dem Gerät prüfen.
- AirPlay: Bonjour-Status und Netzwerkprofil prüfen.

## Lizenz

- Siehe `LICENSE` für Lizenztext und Third-Party-Hinweise.
