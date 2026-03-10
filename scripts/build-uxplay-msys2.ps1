param(
    [string]$UxPlayDir = "vendor/UxPlay",
    [string]$OutputZip = "vendor/uxplay/dist/uxplay-windows.zip",
    [string]$MsysRoot = "C:/msys64"
)

$ErrorActionPreference = "Stop"

function Resolve-PathRelative([string]$PathText) {
    if ([System.IO.Path]::IsPathRooted($PathText)) {
        return $PathText
    }

    return Join-Path (Get-Location) $PathText
}

function ConvertTo-MsysPath([string]$WindowsPath) {
    $full = [System.IO.Path]::GetFullPath($WindowsPath)
    $drive = $full.Substring(0, 1).ToLowerInvariant()
    $rest = $full.Substring(2).Replace('\', '/')
    return "/$drive$rest"
}

$uxPlayDirResolved = Resolve-PathRelative $UxPlayDir
if (-not (Test-Path $uxPlayDirResolved)) {
    throw "UxPlay source directory not found: $uxPlayDirResolved"
}

$msysBash = Join-Path (Resolve-PathRelative $MsysRoot) "usr\\bin\\bash.exe"
if (-not (Test-Path $msysBash)) {
    throw "MSYS2 bash not found at $msysBash"
}

$bonjourSdkPath = "C:\Program Files\Bonjour SDK"
$hasBonjourSdk = Test-Path $bonjourSdkPath
$fallbackBonjourSdk = Resolve-PathRelative "vendor/uxplay/.bonjour-sdk-fallback"

if (-not $hasBonjourSdk) {
    $systemDnssd = "C:\Windows\System32\dnssd.dll"
    if (-not (Test-Path $systemDnssd)) {
        throw "Neither Bonjour SDK nor C:\Windows\System32\dnssd.dll is available."
    }

    $fallbackInclude = Join-Path $fallbackBonjourSdk "Include"
    $fallbackLibX64 = Join-Path $fallbackBonjourSdk "Lib\\x64"
    New-Item -ItemType Directory -Path $fallbackInclude -Force | Out-Null
    New-Item -ItemType Directory -Path $fallbackLibX64 -Force | Out-Null

    $dnsSdHeader = Join-Path $fallbackInclude "dns_sd.h"
    if (-not (Test-Path $dnsSdHeader)) {
        $dnsHeaderUrl = "https://raw.githubusercontent.com/apple-oss-distributions/mDNSResponder/rel/mDNSResponder-214/mDNSShared/dns_sd.h"
        Write-Host "Downloading dns_sd.h for fallback SDK ..."
        Invoke-WebRequest -Uri $dnsHeaderUrl -OutFile $dnsSdHeader
    }
}

$outputZipResolved = Resolve-PathRelative $OutputZip
$outputDir = Split-Path -Parent $outputZipResolved
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$uxPlayDirMsys = ConvertTo-MsysPath $uxPlayDirResolved
$tmpPackDir = Join-Path $env:TEMP ("uxplay-pack-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmpPackDir -Force | Out-Null
$tmpPackDirMsys = ConvertTo-MsysPath $tmpPackDir
$bonjourSdkMsys = if ($hasBonjourSdk) { ConvertTo-MsysPath $bonjourSdkPath } else { "" }
$fallbackBonjourSdkMsys = ConvertTo-MsysPath $fallbackBonjourSdk

$bashScript = @"
set -euo pipefail
export MSYSTEM=UCRT64
export CHERE_INVOKING=1
export PATH=/ucrt64/bin:/usr/bin:$PATH

pacman -S --needed --noconfirm mingw-w64-ucrt-x86_64-cmake mingw-w64-ucrt-x86_64-gcc mingw-w64-ucrt-x86_64-ninja mingw-w64-ucrt-x86_64-binutils mingw-w64-ucrt-x86_64-tools mingw-w64-ucrt-x86_64-libplist mingw-w64-ucrt-x86_64-gstreamer mingw-w64-ucrt-x86_64-gst-plugins-base mingw-w64-ucrt-x86_64-gst-plugins-good mingw-w64-ucrt-x86_64-gst-plugins-bad mingw-w64-ucrt-x86_64-gst-libav

if pkg-config --exists avahi-compat-libdns_sd; then
    echo "Using avahi-compat-libdns_sd from MSYS2."
else
    echo "avahi-compat-libdns_sd not found in MSYS2 pkg-config." >&2
    if [ -d "$bonjourSdkMsys" ]; then
        export BONJOUR_SDK_HOME="C:/Program Files/Bonjour SDK"
        echo "Falling back to BONJOUR_SDK_HOME=$BONJOUR_SDK_HOME"
    elif [ -d "$fallbackBonjourSdkMsys" ]; then
        if [ ! -f "$fallbackBonjourSdkMsys/Lib/x64/dnssd.lib" ]; then
            echo "Generating dnssd.lib from system dnssd.dll ..."
            cd "$fallbackBonjourSdkMsys/Lib/x64"
            gendef /c/Windows/System32/dnssd.dll
            dlltool -d dnssd.def -D dnssd.dll -l dnssd.lib
        fi
        export BONJOUR_SDK_HOME="$fallbackBonjourSdkMsys"
        echo "Using generated fallback BONJOUR_SDK_HOME=$BONJOUR_SDK_HOME"
    else
        echo "Neither avahi-compat-libdns_sd nor Bonjour SDK is available. Install Bonjour SDK or provide avahi package." >&2
        exit 2
    fi
fi

cd "$uxPlayDirMsys"
rm -rf build
mkdir -p build
cd build
cmake -G Ninja ..
ninja

mkdir -p "$tmpPackDirMsys"
cp -f uxplay.exe "$tmpPackDirMsys/uxplay.exe"

# Bundle all UCRT runtime DLLs to avoid missing transitive dependencies on clean systems.
cp -f /ucrt64/bin/*.dll "$tmpPackDirMsys/" || true

# Provide plugin folder so runtime can discover codecs/sinks.
mkdir -p "$tmpPackDirMsys/lib/gstreamer-1.0"
cp -f /ucrt64/lib/gstreamer-1.0/*.dll "$tmpPackDirMsys/lib/gstreamer-1.0/" || true
"@

Write-Host "Building UxPlay with MSYS2/UCRT64 ..."
$bashScriptPath = Join-Path $env:TEMP ("build-uxplay-" + [System.Guid]::NewGuid().ToString("N") + ".sh")
$bashScriptLf = $bashScript -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($bashScriptPath, $bashScriptLf, [System.Text.Encoding]::ASCII)
$bashScriptPathMsys = ConvertTo-MsysPath $bashScriptPath

& $msysBash -lc "bash '$bashScriptPathMsys'"
$exitCode = $LASTEXITCODE

if (Test-Path $bashScriptPath) {
    Remove-Item -Path $bashScriptPath -Force
}

if ($exitCode -ne 0) {
    throw "MSYS2 UxPlay build failed with exit code $exitCode"
}

if (Test-Path $outputZipResolved) {
    Remove-Item $outputZipResolved -Force
}

Write-Host "Packing local UxPlay archive: $outputZipResolved"
Compress-Archive -Path (Join-Path $tmpPackDir "*") -DestinationPath $outputZipResolved -Force

Remove-Item -Path $tmpPackDir -Recurse -Force

Write-Host "Done. Local UxPlay archive created at: $outputZipResolved"
