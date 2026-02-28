param(
    [string]$Platform = "x64",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

# --- Worktree discovery ----------------------------------------------------------

$raw = git -C $repoRoot worktree list --porcelain 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[dev] Failed to list worktrees." -ForegroundColor Red
    exit 1
}

$worktrees = @()
$entry = @{}

foreach ($line in $raw) {
    if ($line -match '^worktree (.+)$') {
        if ($entry.Count -gt 0) { $worktrees += [PSCustomObject]$entry }
        $entry = @{ Path = $Matches[1] }
    }
    elseif ($line -match '^branch refs/heads/(.+)$') {
        $entry.Branch = $Matches[1]
    }
    elseif ($line -match '^HEAD ([0-9a-f]+)$') {
        $entry.Commit = $Matches[1].Substring(0, 7)
    }
}
if ($entry.Count -gt 0) { $worktrees += [PSCustomObject]$entry }

if ($worktrees.Count -eq 0) {
    Write-Host "[dev] No worktrees found." -ForegroundColor Red
    exit 1
}

# --- Worktree selection ----------------------------------------------------------

$selectedPath = $repoRoot  # default if only one worktree

if ($worktrees.Count -gt 1) {
    function Show-Menu {
        param([int]$sel)
        $Host.UI.RawUI.CursorPosition = $script:menuOrigin

        for ($i = 0; $i -lt $worktrees.Count; $i++) {
            $wt     = $worktrees[$i]
            $name   = Split-Path $wt.Path -Leaf
            $branch = if ($wt.Branch) { $wt.Branch } else { "detached" }
            $commit = if ($wt.Commit) { $wt.Commit } else { "???????" }
            $label  = "$name  ($branch @ $commit)"

            if ($i -eq $sel) {
                Write-Host "  > " -NoNewline -ForegroundColor Cyan
                Write-Host $label -ForegroundColor White
            } else {
                Write-Host "    $label" -ForegroundColor DarkGray
            }
        }
    }

    Write-Host ""
    Write-Host "  Select a worktree:" -ForegroundColor Yellow
    Write-Host "  [Up/Down] navigate  [Enter] select  [Esc] cancel" -ForegroundColor DarkGray
    Write-Host ""

    $script:menuOrigin = $Host.UI.RawUI.CursorPosition
    $sel = 0
    Show-Menu -sel $sel

    while ($true) {
        $key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

        switch ($key.VirtualKeyCode) {
            38 { if ($sel -gt 0)                     { $sel-- } }   # Up
            40 { if ($sel -lt $worktrees.Count - 1)  { $sel++ } }   # Down
            27 { Write-Host "`n  Cancelled." -ForegroundColor DarkGray; exit 0 }
        }

        if ($key.VirtualKeyCode -eq 13) { break }   # Enter

        Show-Menu -sel $sel
    }

    $selectedPath = $worktrees[$sel].Path
}

$selectedName   = Split-Path $selectedPath -Leaf
$selectedBranch = ($worktrees | Where-Object { $_.Path -eq $selectedPath }).Branch
if (-not $selectedBranch) { $selectedBranch = "detached" }

$exePath = Join-Path $selectedPath "bin\$Platform\$Configuration\net8.0-windows10.0.19041.0\Tactadile.exe"

# --- Helpers ---------------------------------------------------------------------

function Stop-Tactadile {
    $proc = Get-Process -Name "Tactadile" -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "[dev] Stopping Tactadile..." -ForegroundColor Yellow
        $proc | Stop-Process -Force
        $proc | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    }
}

function Build-Tactadile {
    Write-Host "[dev] Building..." -ForegroundColor Yellow
    dotnet build "$selectedPath" -p:Platform=$Platform -c $Configuration -v quiet
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

# --- Initial build & launch -----------------------------------------------------

Write-Host ""
Write-Host "[dev] Tactadile dev mode" -ForegroundColor Cyan
Write-Host "[dev] Worktree: $selectedName ($selectedBranch)" -ForegroundColor Cyan
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

# --- File watcher ----------------------------------------------------------------

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $selectedPath
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
