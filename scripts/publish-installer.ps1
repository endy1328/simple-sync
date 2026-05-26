param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [switch]$SkipInno
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot "artifacts"
$stagingDir = Join-Path $artifactsRoot "installer\staging"
$distDir = Join-Path $repoRoot "dist"
$installerScript = Join-Path $repoRoot "installer\simple-sync.iss"
$versionPath = Join-Path $repoRoot "VERSION"

if ([string]::IsNullOrWhiteSpace($Version)) {
    if (Test-Path -LiteralPath $versionPath) {
        $Version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    }
    else {
        $Version = "1.1.0"
    }
}

$assemblyVersion = "$Version.0"

function Find-InnoCompiler {
    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
    }

    return $null
}

if (Test-Path -LiteralPath $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $stagingDir, $distDir | Out-Null

dotnet publish $repoRoot `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:AssemblyVersion=$assemblyVersion `
    -p:FileVersion=$assemblyVersion `
    -p:InformationalVersion=$Version `
    -o $stagingDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "CHANGELOG.md") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "VERSION") -Destination $stagingDir -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "config.example.toml") -Destination $stagingDir -Force

$stagingAssets = Join-Path $stagingDir "Assets"
$stagingScripts = Join-Path $stagingDir "scripts"
New-Item -ItemType Directory -Force -Path $stagingAssets, $stagingScripts | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot "Assets\simple-sync.ico") -Destination $stagingAssets -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "Assets\simple-sync-256.png") -Destination $stagingAssets -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "Assets\simple-sync.svg") -Destination $stagingAssets -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\simple-sync.ps1") -Destination $stagingScripts -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\simple-sync-robocopy.cmd") -Destination $stagingScripts -Force

if ($SkipInno) {
    Write-Host "Staging complete: $stagingDir"
    return
}

$iscc = Find-InnoCompiler
if (-not $iscc) {
    Write-Host "Staging complete: $stagingDir"
    Write-Host "Inno Setup compiler ISCC.exe was not found."
    Write-Host "Install Inno Setup 6, then run this script again to create dist\simple-sync-setup.exe."
    return
}

$innoArgs = @(
    "/DAppVersion=$Version",
    "/DStagingDir=$stagingDir",
    "/DOutputDir=$distDir",
    $installerScript
)

& $iscc @innoArgs

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compiler failed with exit code $LASTEXITCODE"
}

Write-Host "Installer output: $(Join-Path $distDir 'simple-sync-setup.exe')"
