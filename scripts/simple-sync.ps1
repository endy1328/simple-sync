param(
    [string]$ConfigPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "config.toml"),
    [switch]$Once
)

$ErrorActionPreference = "Stop"

function Read-SimpleSyncConfig {
    param([string]$Path)

    $config = [ordered]@{
        interval_seconds = 10
        pairs = New-Object System.Collections.Generic.List[object]
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Config file not found: $Path"
    }

    $currentPair = $null
    foreach ($rawLine in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $line = Remove-TomlComment $rawLine
        $line = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        if ($line -ieq "[[pairs]]") {
            $currentPair = [ordered]@{
                name = ""
                enabled = $true
                mode = "copy"
                source = ""
                target = ""
            }
            $config.pairs.Add($currentPair)
            continue
        }

        $separator = $line.IndexOf("=")
        if ($separator -lt 0) {
            continue
        }

        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1).Trim()

        if ($null -eq $currentPair) {
            if ($key -ieq "interval_seconds") {
                $seconds = 0
                if ([int]::TryParse($value, [ref]$seconds)) {
                    $config.interval_seconds = [Math]::Max(1, [Math]::Min(86400, $seconds))
                }
            }
            continue
        }

        if ($key -ieq "enabled") {
            $currentPair.enabled = ($value -ieq "true")
        }
        elseif ($key -ieq "name") {
            $currentPair.name = ConvertFrom-TomlString $value
        }
        elseif ($key -ieq "mode") {
            $mode = ConvertFrom-TomlString $value
            $currentPair.mode = if ($mode -ieq "mirror") { "mirror" } else { "copy" }
        }
        elseif ($key -ieq "source") {
            $currentPair.source = ConvertFrom-TomlString $value
        }
        elseif ($key -ieq "target") {
            $currentPair.target = ConvertFrom-TomlString $value
        }
    }

    return $config
}

function Remove-TomlComment {
    param([string]$Line)

    $inString = $false
    for ($i = 0; $i -lt $Line.Length; $i++) {
        if ($Line[$i] -eq '"' -and ($i -eq 0 -or $Line[$i - 1] -ne '\')) {
            $inString = -not $inString
        }
        elseif ($Line[$i] -eq '#' -and -not $inString) {
            return $Line.Substring(0, $i)
        }
    }

    return $Line
}

function ConvertFrom-TomlString {
    param([string]$Value)

    $text = $Value.Trim()
    if ($text.Length -ge 2 -and $text[0] -eq '"' -and $text[$text.Length - 1] -eq '"') {
        $text = $text.Substring(1, $text.Length - 2)
    }

    return $text.Replace('\"', '"').Replace('\\', '\')
}

function Test-IsSameOrChildPath {
    param(
        [string]$Parent,
        [string]$Candidate
    )

    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    return $candidatePath.Equals($parentPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $candidatePath.StartsWith($parentPath, [System.StringComparison]::OrdinalIgnoreCase)
}

function Invoke-SimpleSyncPair {
    param(
        [string]$Name,
        [string]$Mode,
        [string]$Source,
        [string]$Target
    )

    if ([string]::IsNullOrWhiteSpace($Source) -or [string]::IsNullOrWhiteSpace($Target)) {
        Write-Host "Skip: source or target path is empty"
        return
    }

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        Write-Host "Skip: source not found: $Source"
        return
    }

    if (Test-IsSameOrChildPath -Parent $Source -Candidate $Target) {
        Write-Host "Skip: target is same as source or inside source: $Target"
        return
    }

    New-Item -ItemType Directory -Force -Path $Target | Out-Null

    $pairName = if ([string]::IsNullOrWhiteSpace($Name)) { "Pair" } else { $Name }
    $normalizedMode = if ($Mode -ieq "mirror") { "mirror" } else { "copy" }
    $robocopyMode = if ($normalizedMode -eq "mirror") { "/MIR" } else { "/E" }

    Write-Host "[$pairName] Mode: $normalizedMode"
    Write-Host "[$pairName] Sync: $Source -> $Target"
    & robocopy $Source $Target $robocopyMode /COPY:DAT /DCOPY:T /R:1 /W:1 /NFL /NDL /NP
    $exitCode = $LASTEXITCODE

    if ($exitCode -le 7) {
        Write-Host "[$pairName] Done: robocopy exit code $exitCode"
    }
    else {
        Write-Host "[$pairName] Failed: robocopy exit code $exitCode"
    }
}

function Invoke-SimpleSync {
    param([object]$Config)

    foreach ($pair in $Config.pairs) {
        if (-not $pair.enabled) {
            continue
        }

        Invoke-SimpleSyncPair -Name $pair.name -Mode $pair.mode -Source $pair.source -Target $pair.target
    }
}

Write-Host "simple sync PowerShell fallback"
Write-Host "Config: $ConfigPath"

do {
    $config = Read-SimpleSyncConfig -Path $ConfigPath
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Start"
    Invoke-SimpleSync -Config $config
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Waiting $($config.interval_seconds) sec"

    if (-not $Once) {
        Start-Sleep -Seconds $config.interval_seconds
    }
} while (-not $Once)
