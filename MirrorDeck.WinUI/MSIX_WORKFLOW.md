# MirrorDeck MSIX Workflow

`MirrorDeck.Package` is now included as a packaging project scaffold in the repository.

## Included projects

- `MirrorDeck.WinUI` - app runtime
- `MirrorDeck.Package` - MSIX packaging project (`.wapproj`)
- `MirrorDeck.Bootstrapper` - optional installer launcher for component preselection

## Build MSIX

From repo root:

```bat
build.bat msix
```

Artifacts are placed in `dist/msix/`.

## Signing

Before production release:

1. Replace temporary package certificate settings in `MirrorDeck.Package.wapproj`.
2. Use your real publisher identity in `MirrorDeck.Package/Package.appxmanifest`.
3. Sign package in CI/CD with secure certificate management.

## Installer strategy

- MSIX path: package `MirrorDeck.WinUI` as full-trust desktop app.
- Classic path: `script.iss` with selectable components (`uxplay`, `scrcpy`, `bonjour`).
- Optional `MirrorDeck.Bootstrapper`: launches installer with preselected components.
