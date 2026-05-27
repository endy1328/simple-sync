param(
    [string]$Version = "",
    [string]$Repository = "endy1328/simple-sync",
    [string]$Target = "main",
    [string]$InstallerPath = "",
    [string]$NotesPath = "",
    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $repoRoot "VERSION"

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw "VERSION file was not found: $versionFile"
    }

    $Version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version is empty."
}

$tag = "v$Version"

if ([string]::IsNullOrWhiteSpace($InstallerPath)) {
    $InstallerPath = Join-Path $repoRoot "dist\simple-sync-setup.exe"
}
elseif (-not [System.IO.Path]::IsPathRooted($InstallerPath)) {
    $InstallerPath = Join-Path $repoRoot $InstallerPath
}

if ([string]::IsNullOrWhiteSpace($NotesPath)) {
    $NotesPath = Join-Path $repoRoot "release-notes-$Version.md"
}
elseif (-not [System.IO.Path]::IsPathRooted($NotesPath)) {
    $NotesPath = Join-Path $repoRoot $NotesPath
}

if (-not (Test-Path -LiteralPath $InstallerPath)) {
    throw "Installer was not found: $InstallerPath. Run scripts\publish-installer.ps1 first."
}

if (-not (Test-Path -LiteralPath $NotesPath)) {
    throw "Release notes were not found: $NotesPath"
}

function Find-GitHubCli {
    $command = Get-Command "gh" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "${env:ProgramFiles}\GitHub CLI\gh.exe",
        "${env:ProgramFiles(x86)}\GitHub CLI\gh.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    return $null
}

$gh = Find-GitHubCli
if (-not $gh) {
    throw "GitHub CLI was not found. Install it, then run gh auth login."
}

$hash = Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256

Write-Host "Repository: $Repository"
Write-Host "Target:     $Target"
Write-Host "Tag:        $tag"
Write-Host "Installer:  $InstallerPath"
Write-Host "Notes:      $NotesPath"
Write-Host "SHA256:     $($hash.Hash)"

$ghArgs = @(
    "release",
    "create",
    $tag,
    $InstallerPath,
    "--repo",
    $Repository,
    "--target",
    $Target,
    "--title",
    "simple sync $Version",
    "--notes-file",
    $NotesPath
)

if ($Draft) {
    $ghArgs += "--draft"
}

if ($Prerelease) {
    $ghArgs += "--prerelease"
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run. GitHub Release will not be created."
    Write-Host "Command:"
    $displayArgs = $ghArgs | ForEach-Object {
        if ($_ -match '\s') {
            "`"$_`""
        }
        else {
            $_
        }
    }
    Write-Host "`"$gh`" $($displayArgs -join ' ')"
    return
}

& $gh auth status
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI is not authenticated. Run gh auth login first."
}

& $gh @ghArgs
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed with exit code $LASTEXITCODE"
}

Write-Host "GitHub Release created: $tag"
