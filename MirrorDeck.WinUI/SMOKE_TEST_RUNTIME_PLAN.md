# MirrorDeck Runtime Smoke-Test Plan

Date: 2026-03-07
Scope: Start minimiert, Tray Exit, Modul on/off, AutoStart on/off, Hotkeys, Fenster-Snapshots.

## Vorbedingungen
- Build erfolgreich: `dotnet build MirrorDeck.sln -c Debug`
- Einstellungen in `Setup & Einstellungen` speicherbar.
- Module installiert (UxPlay/scrcpy/adb/Bonjour), falls die jeweiligen Testfaelle mit echter Session ausgefuehrt werden.

## Testfaelle

1. Start minimiert + Tray-Recover
- Setup:
  - `Minimiert starten = true`
  - `Beim Schliessen in den Tray minimieren = true`
- Schritte:
  - App starten.
  - Pruefen, dass MainWindow nicht im Vordergrund bleibt und Tray-Icon vorhanden ist.
  - Tray-Linksklick oder `App oeffnen` ausfuehren.
- Erwartet:
  - Fenster wird zuverlaessig sichtbar/aktiv.
- Beobachtet:
  - Manuell auszufuehren (GUI-Interaktion erforderlich).

2. Tray Exit -> kontrollierter Shutdown
- Schritte:
  - App laufen lassen, optional aktive Sessions starten.
  - Tray-Menue `Beenden` auswaehlen.
- Erwartet:
  - Shutdown-Splash erscheint, Sessions werden beendet, Tray-Icon verschwindet, App-Prozess beendet sich sauber.
- Beobachtet:
  - Manuell auszufuehren (GUI-Interaktion erforderlich).

3. Modul on/off und Tray-Menue-Dynamik
- Setup A: AirPlay=true, Android=false
- Erwartet A:
  - Nur AirPlay-Aktionen im Tray sichtbar.
- Setup B: AirPlay=false, Android=true
- Erwartet B:
  - Nur Android-Aktionen im Tray sichtbar.
- Setup C: beide false
- Erwartet C:
  - Keine AirPlay-/Android-Aktionsgruppen, `Alle Sessions stoppen` ausgeblendet.
- Beobachtet:
  - Manuell auszufuehren (GUI-Interaktion erforderlich).

4. AutoStart on/off
- Setup A:
  - AirPlay-Modul aktiv + `AirPlay bei Programmstart automatisch starten = true`
  - Android-Modul aktiv + `Android bei Programmstart automatisch starten und verbinden = true`
- Schritte:
  - App neu starten, Logs/Tray-Status beobachten.
- Erwartet:
  - Entsprechende Module starten nur wenn Modul aktiv + AutoStart true.
- Beobachtet:
  - Manuell auszufuehren (Netzwerk/Geraeteabhaengig).

5. Shortcut Snapshot (global)
- Setup:
  - `Shortcut Snapshot` setzen (z. B. `Ctrl+Shift+S`).
  - Mindestens ein Mirroring-Fenster aktiv.
- Schritte:
  - Shortcut ausloesen, auch wenn MainWindow minimiert/verdeckt ist.
- Erwartet:
  - Nur Fenster-Snapshots der Mirroring-Fenster werden erzeugt.
  - Keine Desktop-Aufnahme mit ueberlagernden Fremdfenstern.
- Beobachtet:
  - Manuell auszufuehren (GUI-Interaktion erforderlich).

6. Shortcut Pause/Play (global)
- Setup:
  - `Shortcut Pause/Play` setzen (z. B. `Ctrl+Shift+P`).
  - Eine oder beide Sessions aktiv.
- Schritte:
  - Shortcut ausloesen.
- Erwartet:
  - Aktive Sessions pausieren bzw. werden wieder fortgesetzt.
- Beobachtet:
  - Manuell auszufuehren (GUI-Interaktion erforderlich).

## Automatisiert verifiziert in dieser Session
- Build nach Snapshot/Shortcut-Aenderungen: erfolgreich.
- Build nach Splash-Redesign Schritt 1: erfolgreich.
- Build nach Splash-Redesign Schritt 2: erfolgreich.

## Hinweise
- Leere Shortcut-Felder deaktivieren den jeweiligen globalen Hotkey.
- Ungueltige Shortcut-Formate werden ignoriert und im Log vermerkt.

## 7. Visueller Lauf (Fonts + DPI)

Ziel:
- Pro Seite mindestens 1 Screenshot erstellen.
- Rendering-Unterschiede bei Fonts, DPI-Skalierung und Layout frueh erkennen.

### Testmatrix

- DPI 100% (96 DPI), Fensterbreite ca. 1366
- DPI 125% (120 DPI), Fensterbreite ca. 1366
- DPI 150% (144 DPI), Fensterbreite ca. 1366

Optional:
- 1920er Breite bei 100% fuer Desktop-Layout
- 1280er Breite bei 150% fuer enge Darstellung

### Screenshots pro Lauf

Dateinamen-Schema:
- `<seite>_<dpi>_<zustand>.png`
- Beispiel: `dashboard_125_idle.png`

Pflichtseiten:
- `dashboard`
- `airplay`
- `android`
- `setup`
- `logs`
- `help`

Zusaetzlich (wenn reproduzierbar):
- `startup_overlay`
- `shutdown_overlay`

### Checkliste pro Seite

1. Typografie
- Umlaute (ae/oe/ue vs. aequivalente) korrekt dargestellt.
- Keine abgeschnittenen Ueberschriften oder Status-Chips.
- Schriftstaerken konsistent (Titel/Body/Muted).

2. Layout
- Titelzeile sauber: Titel links, Chips rechts, vertikal mittig.
- Aktionsflaechen gleichmaessig verteilt, gleiche Aussenabstaende links/rechts.
- Kein Clipping in Buttons (insb. Snapshot-Text).

3. Abstaende und Ausrichtung
- Einheitliche Innenabstaende in Cards.
- Icon + Text in Action-Tiles horizontal zentriert.
- Status-Chips in einer Zeile ohne Ueberlappung.

4. Titelzeile/Fensterrahmen
- Keine Farbunterbrechung zwischen Versionstext und Systembuttons.
- Versionslabel sitzt links vom `X` mit stabilem Abstand bei verschiedenen DPI.

5. Funktionale Sichtpruefung
- Navigation zwischen allen Seiten ohne sichtbares Flackern.
- Scrollbereiche nur dort, wo erwartet (keine unerwarteten horizontalen Scrollbars).

### Ergebnisprotokoll

Pro Kombination (Seite + DPI):
- Status: `PASS` / `WARN` / `FAIL`
- Kurznotiz: max. 1-2 Saetze
- Screenshot-Datei referenzieren

Beispiel:
- `dashboard @125%: PASS - Chips und 3er-Action-Grid sauber ausgerichtet. (dashboard_125_idle.png)`

### Abbruchkriterien

Bei folgenden Punkten direkt als `FAIL` markieren:
- Text abgeschnitten (Titel, Buttons, Chips)
- Umlaut-/Zeichenfehler in sichtbaren Strings
- Ueberlappung von Chips, Buttons oder Header-Elementen
- Versionstext kollidiert mit Systembuttons
