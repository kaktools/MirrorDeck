param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$packageProject = Join-Path $root "MirrorDeck.Package\MirrorDeck.Package.wapproj"
$output = Join-Path $root "dist\msix\"
$desktopBridge = Join-Path $env:ProgramFiles "Microsoft Visual Studio"

if (-not (Test-Path $packageProject)) {
    throw "MirrorDeck.Package.wapproj not found."
}

if (-not (Test-Path "C:\Program Files\dotnet\sdk\$((dotnet --version).Trim())\Microsoft\DesktopBridge\Microsoft.DesktopBridge.props")) {
    Write-Warning "DesktopBridge tools not found for CLI build. Install Visual Studio Windows Application Packaging workload."
    return
}

Write-Host "Building MirrorDeck.Package MSIX..."
dotnet build $packageProject -c $Configuration /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxPackageDir=$output

Write-Host "MSIX build complete. Output: $output"
