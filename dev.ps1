param(
    [string]$Platform = "x64",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$exePath = Join-Path $repoRoot "bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\Tactadile.exe"

function Stop-Tactadile {
    $proc = Get-Process -Name "Tactadile" -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "[dev] Stopping Tactadile..." -ForegroundColor Yellow
        $proc | Stop-Process -Force
        # Wait for process to fully exit and release mutex
        $proc | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    }
}

function Build-Tactadile {
    Write-Host "[dev] Building..." -ForegroundColor Yellow
    dotnet build "$repoRoot" -p:Platform=$Platform -c $Configuration -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[dev] Build FAILED" -ForegroundColor Red
        return $false
    }
    Write-Host "[dev] Build succeeded" -ForegroundColor Green
    return $true
}

function Start-Tactadile {
    if (-not (Test-Path $exePath)) {
        Write-Host "[dev] Exe not found: $exePath" -ForegroundColor Red
        return
    }
    Write-Host "[dev] Launching Tactadile" -ForegroundColor Green
    Start-Process $exePath -WorkingDirectory (Split-Path $exePath)
}

# --- Initial build & launch ---
Write-Host "[dev] Tactadile dev mode" -ForegroundColor Cyan
Write-Host "[dev] Platform: $Platform | Config: $Configuration" -ForegroundColor Cyan
Write-Host ""

Stop-Tactadile

if (-not (Build-Tactadile)) {
    Write-Host "[dev] Initial build failed. Fix errors and try again." -ForegroundColor Red
    exit 1
}

Start-Tactadile
Write-Host ""
Write-Host "[dev] Watching for changes... (Ctrl+C to stop)" -ForegroundColor Cyan
Write-Host ""

# --- File watcher setup ---
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $repoRoot
$watcher.IncludeSubdirectories = $true
$watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite -bor [System.IO.NotifyFilters]::FileName

try {
    while ($true) {
        $result = $watcher.WaitForChanged(
            [System.IO.WatcherChangeTypes]::Changed -bor
            [System.IO.WatcherChangeTypes]::Created -bor
            [System.IO.WatcherChangeTypes]::Renamed
        )

        # Filter: only .cs and .xaml files, skip bin/obj
        if ($result.Name -match '[\\/](bin|obj)[\\/]') { continue }
        if ($result.Name -notmatch '\.(cs|xaml)$') { continue }

        # Debounce: drain additional events for 500ms
        $deadline = [DateTime]::UtcNow.AddMilliseconds(500)
        while ([DateTime]::UtcNow -lt $deadline) {
            $watcher.WaitForChanged([System.IO.WatcherChangeTypes]::All, 100) | Out-Null
        }

        Write-Host ""
        Write-Host "[dev] Change detected: $($result.Name)" -ForegroundColor Cyan

        Stop-Tactadile

        if (Build-Tactadile) {
            Start-Tactadile
        } else {
            Write-Host "[dev] Waiting for next change..." -ForegroundColor Yellow
        }
    }
} finally {
    Stop-Tactadile
    $watcher.Dispose()
    Write-Host "[dev] Stopped." -ForegroundColor Yellow
}
