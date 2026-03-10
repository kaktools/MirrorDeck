param(
    [string]$RepoUrl = "https://github.com/FDH2/UxPlay.git",
    [string]$TargetDir = "vendor/UxPlay",
    [string]$Ref = "master"
)

$ErrorActionPreference = "Stop"

function Resolve-PathRelative([string]$PathText) {
    if ([System.IO.Path]::IsPathRooted($PathText)) {
        return $PathText
    }

    return Join-Path (Get-Location) $PathText
}

$target = Resolve-PathRelative $TargetDir
$targetParent = Split-Path -Parent $target
if (-not (Test-Path $targetParent)) {
    New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git was not found. Install Git and try again."
}

if (-not (Test-Path (Join-Path $target ".git"))) {
    Write-Host "Cloning UxPlay into $target ..."
    git clone $RepoUrl $target
}
else {
    Write-Host "Refreshing existing UxPlay checkout in $target ..."
    git -C $target fetch --all --tags --prune
}

Write-Host "Checking out $Ref ..."
git -C $target checkout $Ref
if ($Ref -eq "master" -or $Ref -eq "main") {
    git -C $target pull --ff-only
}

Write-Host "UxPlay source is ready at: $target"
Write-Host "Current commit:"
git -C $target rev-parse --short HEAD
