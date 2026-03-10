param(
    [string]$Tag = "latest",
    [string]$WorkRoot = "vendor/uxplay/src",
    [string]$OutputZip = "vendor/uxplay/dist/uxplay-windows.zip"
)

$ErrorActionPreference = "Stop"

function Resolve-PathRelative([string]$PathText) {
    if ([System.IO.Path]::IsPathRooted($PathText)) {
        return $PathText
    }

    return Join-Path (Get-Location) $PathText
}

function Get-UxPlayTag([string]$RequestedTag) {
    if ($RequestedTag -and $RequestedTag -ne "latest") {
        return $RequestedTag
    }

    $headers = @{ "User-Agent" = "MirrorDeck-UxPlay-Build" }
    $latest = Invoke-RestMethod -Headers $headers -Uri "https://api.github.com/repos/FDH2/UxPlay/releases/latest"
    if (-not $latest.tag_name) {
        throw "Could not determine latest UxPlay tag from GitHub releases API."
    }

    return [string]$latest.tag_name
}

$resolvedTag = Get-UxPlayTag -RequestedTag $Tag
Write-Host "Using UxPlay release tag: $resolvedTag"

$workRootResolved = Resolve-PathRelative $WorkRoot
if (-not (Test-Path $workRootResolved)) {
    New-Item -ItemType Directory -Path $workRootResolved -Force | Out-Null
}

$sourceDir = Join-Path $workRootResolved ("UxPlay-" + $resolvedTag)
if (Test-Path $sourceDir) {
    Remove-Item -Recurse -Force $sourceDir
}
New-Item -ItemType Directory -Path $sourceDir -Force | Out-Null

$tmpTar = Join-Path $env:TEMP ("uxplay-" + $resolvedTag + ".tar.gz")
$tmpExtract = Join-Path $env:TEMP ("uxplay-src-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmpExtract -Force | Out-Null

try {
    $tarUrl = "https://github.com/FDH2/UxPlay/archive/refs/tags/$resolvedTag.tar.gz"
    Write-Host "Downloading source tarball: $tarUrl"
    Invoke-WebRequest -Uri $tarUrl -OutFile $tmpTar

    Write-Host "Extracting source tarball ..."
    & tar -xzf $tmpTar -C $tmpExtract
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to extract tarball with tar."
    }

    $extractedRoot = Get-ChildItem -Path $tmpExtract -Directory | Select-Object -First 1
    if ($null -eq $extractedRoot) {
        throw "No extracted source directory found in temporary extraction path."
    }

    Copy-Item -Path (Join-Path $extractedRoot.FullName "*") -Destination $sourceDir -Recurse -Force
}
finally {
    if (Test-Path $tmpTar) {
        Remove-Item -Force $tmpTar
    }
    if (Test-Path $tmpExtract) {
        Remove-Item -Recurse -Force $tmpExtract
    }
}

$buildScript = Resolve-PathRelative "scripts/build-uxplay-msys2.ps1"
if (-not (Test-Path $buildScript)) {
    throw "Missing build helper: $buildScript"
}

Write-Host "Building UxPlay from release source ..."
& powershell -ExecutionPolicy Bypass -File $buildScript -UxPlayDir $sourceDir -OutputZip $OutputZip
if ($LASTEXITCODE -ne 0) {
    throw "build-uxplay-msys2.ps1 failed with exit code $LASTEXITCODE"
}

Write-Host "Done. Built local UxPlay bundle from release tag $resolvedTag."