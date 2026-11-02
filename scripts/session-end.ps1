# Session close-out helper.
#
# Validates the MkDocs site, commits every change and pushes to main.
# GitHub Pages rebuilds automatically.
#
# Usage (from the repository root):
#   .\scripts\session-end.ps1 -Message "Session 2026-11-02: translated docs"

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Message,

    [switch]$SkipBuild,
    [switch]$SkipPush
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw 'git was not found in PATH.'
    }

    if (-not $SkipBuild) {
        if (Get-Command python -ErrorAction SilentlyContinue) {
            Write-Host 'Building documentation...' -ForegroundColor Cyan
            & python -m mkdocs build
            if ($LASTEXITCODE -ne 0) { throw 'mkdocs build failed; fix the docs before closing the session.' }
        }
        else {
            Write-Warning 'python not found: skipping the documentation build check.'
        }
    }

    git add -A
    $staged = @(git diff --cached --name-only)

    if ($staged.Count -eq 0) {
        Write-Host 'Nothing to commit.' -ForegroundColor Yellow
    }
    else {
        if (-not ($staged -contains 'docs/status.md')) {
            Write-Warning 'docs/status.md was not modified. Did you mark the finished tasks and add new ones?'
        }

        git commit -m $Message | Out-Null
        Write-Host "Committed: $Message ($($staged.Count) files)" -ForegroundColor Green
    }

    if (-not $SkipPush) {
        git push
        if ($LASTEXITCODE -ne 0) { throw 'git push failed.' }
        Write-Host 'Pushed to origin. GitHub Pages will redeploy.' -ForegroundColor Green
        Write-Host 'Docs: https://albertacedo.github.io/albion-data-project/' -ForegroundColor Cyan
    }
}
finally {
    Pop-Location
}
