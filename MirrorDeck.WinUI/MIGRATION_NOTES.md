# MirrorDeck WinUI 3 Migration Notes

## Scope of this first runnable version

This folder contains a new WinUI 3 + MVVM app shell named `MirrorDeck`.
It upgrades the former Python tray launcher architecture into a C# service-based control center foundation for:

- AirPlay via UxPlay
- Android mirroring via scrcpy/adb

## Implemented structure

- `App.xaml(.cs)`: app bootstrap, DI container, startup flow.
- `MainWindow.xaml(.cs)`: NavigationView shell and tray-first close behavior.
- `Models/`: settings, status, profiles, log entry.
- `ViewModels/`: dashboard, airplay, android, settings, logs.
- `Views/`: Dashboard, AirPlay, Android, Settings, Logs pages.
- `Services/Interfaces/`: contracts for all core services.
- `Services/`: UxPlay, scrcpy, adb, bonjour, download, tray, autostart.
- `ProcessRunner/ProcessRunner.cs`: async process lifecycle with stdout/stderr streaming.
- `DependencyManagement/DependencyService.cs`: dynamic GitHub release lookup + download + zip extraction + version markers.
- `UpdateManagement/GitHubReleaseClient.cs`: release API integration.
- `Settings/SettingsService.cs`: JSON settings in `%APPDATA%\\MirrorDeck\\settings.json`.
- `Logging/LoggingService.cs`: in-memory + file logging `%APPDATA%\\MirrorDeck\\mirrordeck.log`.

## Reused logic direction from legacy app

Concepts ported from legacy `tray.py` design:

- Process start/stop/restart discipline for external tools.
- Bonjour state checks and restart service handling.
- Dependency health checks and release-driven update logic.
- Tray-first behavior as an app lifecycle strategy.

## What works now

- Compilable WinUI 3 app (`dotnet build` successful).
- Modern multi-page control center shell with dashboard cards and quick actions.
- Service selection (AirPlay, Android, or both) with persistent settings.
- UxPlay and scrcpy services can start/stop/restart external processes.
- adb device discovery + TCP connect/disconnect.
- Background-only mode hides external tool windows to keep MirrorDeck as the control UI.
- UxPlay controls include pause/resume and screenshot capture.
- Dependency setup flow can fetch latest release archives dynamically and extract.
- Settings and logs are persisted in AppData.
- Window close can minimize/hide instead of exiting when configured.
- Native tasktray integration with live status rows and quick actions.
- Setup Assistant page for Bonjour diagnostics, download, install, start/restart.

## Important next increment

1. Refine tray icon badges (color states) and notifications for status transitions.
2. Add optional tool update checks and SHA verification UI in setup.
3. Add dedicated `MirrorDeck.Package` packaging project for signed MSIX output.

## Build

```powershell
Set-Location e:\Repos\MirrorDeck\MirrorDeck.WinUI
 dotnet build
```

## Packaging direction

- Keep this app project as WinUI desktop core.
- Add MSIX packaging project (or CI installer) in the next phase.
- Move dependency setup to first-run assistant page + elevated tasks only for service/install operations.
