$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$processHelpers = Join-Path $PSScriptRoot 'process-management.ps1'
. $processHelpers
Assert-AutoInsightPortAvailable -Port 5173
Set-Location (Join-Path $projectRoot 'frontend')
Write-Host 'Starting the frontend in this terminal. Press Ctrl+C to stop it.'
npm run dev -- --host localhost --port 5173
