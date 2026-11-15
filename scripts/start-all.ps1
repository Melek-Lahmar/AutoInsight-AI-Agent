$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path $projectRoot 'backend/AutoInsight.Api/AutoInsight.Api.csproj'
$backend = Start-Process dotnet -ArgumentList @('run','--project',('"' + $backendProject + '"'),'--urls','http://localhost:5000') -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
$frontend = Start-Process npm.cmd -ArgumentList @('run','dev','--','--host','localhost','--port','5173') -WorkingDirectory (Join-Path $projectRoot 'frontend') -WindowStyle Hidden -PassThru
Write-Host "Backend PID: $($backend.Id)  http://localhost:5000"
Write-Host "Frontend PID: $($frontend.Id) http://localhost:5173"
Write-Host 'Run ./scripts/stop-all.ps1 when finished.'
