# MirrorDeck Release Notes

## 1.3.0

### Neu

- Versionsanzeige in die obere Titelzeile verschoben (links neben Systembuttons).
- Visueller QA-Lauf mit DPI-/Font-Checkliste dokumentiert.
- Ergebnisvorlage für visuelle Tests unter `artifacts/visual-qa/2026-03-08/results-template.md`.

### Geändert

- Dashboard-Layout überarbeitet.
- Aktionsflächen in den Bereichen AirPlay, Android und Globale Aktionen gleichmäßig aufgeteilt.
- Status-Chips im Dashboard in die Kopfzeilen verschoben (Titel links, Chips rechts).
- AirPlay-/Android-Seitenkopf überarbeitet.
- Status-Chips in die Titelzeile nach oben rechts verschoben.
- Hinweise und Beschriftungen angepasst.
- "AirPlay-Receiver" auf "AirPlay-Mirror" umgestellt.
- Snapshot-Beschriftung gekürzt, um Clipping zu vermeiden.
- Android-Seite: "Kurzer Hinweis" aktualisiert und klarer Verweis auf Hilfe-Anleitung gesetzt.
- AirPlay- und Android-Seite um einen zusätzlichen Schalter `Auto-Restart` erweitert.

### Verbessert

- Snapshot-Erzeugung gehärtet.
- Capture-Pfad auf Mirroring-Fenster fokussiert (AirPlay/scrcpy), kein Desktop-Fallback im aktiven Workflow.
- Trefferauswahl für Fenster-Capture verbessert (Titel-Präferenz + robuste Sichtbarkeits-/Größenheuristik).
- Deutsche UI-Texte bereinigt und vereinheitlicht (Umlaut-/ASCII-Fallbacks in Quelltexten entfernt).
- Auto-Restart robuster gemacht: Fehlerpfade sind abgefangen, damit die App stabil bleibt.

### Behoben

- Unsaubere Zentrierung der Snapshot-Aktionsfläche im Dashboard korrigiert.
- Visuelle Farbunterbrechung in der Titelzeile durch vereinheitlichte Darstellung reduziert.
- Auto-Restart jetzt mit klaren Leitplanken: maximal 10 Versuche im Abstand von 5 Sekunden, danach Stopp mit Logeintrag.

### Hinweise

- Alte Schreibweisen in `obj/`-Artefakten können weiterhin auftauchen; relevant ist der Quellstand in `Views/`, `ViewModels/`, `Services/`.
- Die Build-Pipeline erzeugt Installer und Portable-Output über `build.bat installer`.

## 1.2.1 - 2026-03-07

### Neu

- Versionsanzeige unten rechts im Start-/Exit-Screen.
- Versionsanzeige unten rechts im Hauptfenster.
- Start-Trace-Log (`%APPDATA%\MirrorDeck\startup-trace.log`) für gezielte Startup-Diagnose.
- Einheitliches Sidebar-Icon-Set (inkl. Desktop-/Tablet-orientierter Moduleinträge).

### Geändert

- Primärfarbe und UI-System auf `#162331` abgestimmt.
- Start-/Shutdown-Screens umfassend überarbeitet (Verläufe, Licht-/Signal-Look, Fortschritt, Sequenzen).
- Action-Karten kompakter gestaltet, damit das Fenster kleiner nutzbar bleibt.
- Doppelte Titelleiste entfernt (stabile System-Titlebar-Variante).
- README und Projektdokumentation auf Deutsch aktualisiert.

### Verbessert

- Einstellungen speichern sich jetzt sofort bei Änderung (kein separater Speichern-Button mehr).
- Close-Verhalten robuster.
- Bei deaktivierter Tray-Minimierung wird sauber beendet.
- Falls Tray nicht bereit ist, wird ebenfalls kontrolliert beendet statt "unsichtbar" weiterzulaufen.
- Single-Instance-Verhalten verbessert.
- Zweiter Start erzeugt keine neue Instanz, sondern bringt die vorhandene App in den Vordergrund.
- Log-Strategie überarbeitet.
- Tagesbasierte Logs unter `%APPDATA%\MirrorDeck\logs`.
- Automatische Retention (älter als 3 Tage wird gelöscht).
- Dateigrößenbegrenzung, damit Logs nicht endlos wachsen.

### Behoben

- Mehrere Startup-Absturzursachen entschärft (Logging-I/O und UI-Lifecycle-Härtung).
- Schwarzes Zwischenfenster/harte Abbrüche nach dem Startscreen deutlich reduziert.

### Wichtiger Hinweis

- Der separate First-Start-Popup-Dialog ist derzeit im Stabilitätsmodus deaktiviert,
  weil dieser auf einigen Systemen einen nativen WinUI-Absturz auslösen konnte.
- Modul-Aktivierung erfolgt temporär direkt über Setup/Einstellungen.

## Upgrade-Hinweise

1. Bestehende Installation mit aktuellem Installer überinstallieren.
2. App starten und in Setup/Einstellungen die gewünschten Module prüfen.
3. Einmal Close-Verhalten testen (Tray an/aus).
4. Optional Android- und AirPlay-Quickcheck durchführen.

## Bekannte Einschränkungen

- Download von Abhängigkeiten benötigt Internetzugang.
- Externes Modulverhalten kann je nach Netzwerk, Treiber und Firewall variieren.

## Artefakte

- Installer: `dist/MirrorDeck-Setup-1.2.1.exe`
- Portable: `dist/portable/MirrorDeck/`
