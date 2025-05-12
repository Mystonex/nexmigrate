<#
.SYNOPSIS
  Bootstrap installer & launcher for nexmigrate.

.DESCRIPTION
  1. Installs .NET 8 Desktop Runtime via winget if needed.
  2. Downloads the nexmigrate.exe from GitHub Releases.
  3. Runs nexmigrate.exe.
#>

param(
  [string]$ExeUrl = 'https://github.com/Mystonex/nexmigrate/releases/download/v0.0.1/nexmigrate.exe'
)

# 1) Ensure .NET 8 Desktop Runtime is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "→ .NET 8 not detected. Installing Desktop Runtime via winget..."
    winget install Microsoft.DotNet.DesktopRuntime.8 `
        --accept-package-agreements --accept-source-agreements
}

# 2) Download the EXE into TEMP
$destination = Join-Path $env:TEMP 'nexmigrate.exe'
Write-Host "→ Downloading nexmigrate to $destination..."
Invoke-WebRequest $ExeUrl -UseBasicParsing -OutFile $destination

# 3) Launch the TUI
Write-Host "→ Launching nexmigrate..."
Start-Process $destination -Wait
