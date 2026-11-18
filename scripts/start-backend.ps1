$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$processHelpers = Join-Path $PSScriptRoot 'process-management.ps1'
. $processHelpers
Assert-AutoInsightPortAvailable -Port 5000
Write-Host 'Starting the backend in this terminal. Press Ctrl+C to stop it.'
dotnet run --project (Join-Path $projectRoot 'backend/AutoInsight.Api/AutoInsight.Api.csproj') --urls http://localhost:5000
