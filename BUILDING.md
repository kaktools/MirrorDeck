## Building MirrorDeck

`MirrorDeck` now builds from the WinUI 3 solution and outputs all artifacts to `dist/`.

## Prerequisites

1. .NET 8 SDK
2. Windows App SDK toolchain (via Visual Studio workload)
3. Inno Setup 6 (`ISCC.exe`) for classic installer builds
4. Optional: Visual Studio packaging tools for MSIX signing

## Versioning

- Version is stored in `version.txt`
- Build script can bump patch version automatically

## Build Commands

From repository root:

```bat
build.bat portable
build.bat installer
build.bat msix
build.bat all
build.bat all --bump
```

### Modes

- `portable`: publishes WinUI app to `dist/portable/MirrorDeck`
- `installer`: builds Inno Setup installer with component selection
- `msix`: builds `MirrorDeck.Package` (if packaging project is available)
- `all`: portable + installer + msix

## Installer Component Selection

The Inno installer now supports selectable components:

- `core` (always installed)
- `uxplay` (AirPlay module)
- `scrcpy` (Android module)
- `bonjour` (helper for AirPlay discovery)

Installer downloads latest release assets dynamically from upstream sources where possible.

## Vendored UxPlay Build (Recommended for reliability)

When upstream GitHub releases do not provide a Windows UxPlay asset, you can bundle
your own local build directly into the installer.

### 1. Vendor UxPlay source

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\vendor-uxplay-source.ps1 -Ref v1.73.3
```

This places the source in `vendor\UxPlay`.

### 2. Build and package UxPlay with MSYS2

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-uxplay-msys2.ps1
```

This produces:

- `vendor\uxplay\dist\uxplay-windows.zip`

### 3. Build MirrorDeck installer

```bat
build.bat installer
```

If `vendor\uxplay\dist\uxplay-windows.zip` exists, `script.iss` embeds it and
installer setup uses it first; only if missing will it attempt GitHub download.

## About uxplay.spec

`uxplay.spec` from UxPlay releases is an RPM spec file for Linux/RPM packaging.
It is not a direct recipe to build a native Windows `.exe` installer artifact.

For Windows, the stable path is still: source + MSYS2/UCRT build.

## Simpler Release-Tag Workflow (No Git Clone Needed)

You can build directly from a release tag tarball:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-uxplay-from-release.ps1 -Tag v1.73.3
```

Or always from latest release:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-uxplay-from-release.ps1 -Tag latest
```

The script downloads the release source tarball, builds UxPlay via MSYS2, and writes
`vendor\uxplay\dist\uxplay-windows.zip` for installer embedding.

## Output

- Portable app: `dist/portable/MirrorDeck/`
- Installer: `dist/MirrorDeck-Setup-v<version>.exe`
- MSIX artifacts: `dist/msix/`
