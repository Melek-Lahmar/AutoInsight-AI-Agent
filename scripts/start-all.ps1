$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$processHelpers = Join-Path $PSScriptRoot 'process-management.ps1'
. $processHelpers

$existingState = Get-AutoInsightState
if ($existingState) {
    $hasLiveProcess = @($existingState.Services | Where-Object {
        ($_.Root -and (Test-AutoInsightProcessIdentity -Identity $_.Root)) -or
        ($_.Listener -and (Test-AutoInsightProcessIdentity -Identity $_.Listener))
    }).Count -gt 0
    if ($hasLiveProcess) {
        throw "A tracked AutoInsight instance is already running. Use ./scripts/stop-all.ps1 before starting another instance."
    }
    Remove-AutoInsightState
    Write-Host 'Removed a stale AutoInsight process record.'
}

Assert-AutoInsightPortAvailable -Port 5000
Assert-AutoInsightPortAvailable -Port 5173

$backendProject = Join-Path $projectRoot 'backend/AutoInsight.Api/AutoInsight.Api.csproj'
$state = [pscustomobject]@{
    ProjectRoot = $projectRoot
    CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
    Services = @()
}

try {
    $backend = Start-Process dotnet -ArgumentList @('run','--project',('"' + $backendProject + '"'),'--urls','http://localhost:5000') -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
    $backendService = [pscustomobject]@{ Name = 'backend'; Port = 5000; Root = Get-AutoInsightProcessIdentity -ProcessId $backend.Id; Listener = $null }
    $state.Services += $backendService
    Save-AutoInsightState -State $state

    $frontend = Start-Process npm.cmd -ArgumentList @('run','dev','--','--host','localhost','--port','5173') -WorkingDirectory (Join-Path $projectRoot 'frontend') -WindowStyle Hidden -PassThru
    $frontendService = [pscustomobject]@{ Name = 'frontend'; Port = 5173; Root = Get-AutoInsightProcessIdentity -ProcessId $frontend.Id; Listener = $null }
    $state.Services += $frontendService
    Save-AutoInsightState -State $state

    $backendService.Listener = Wait-AutoInsightListener -Port 5000 -RootProcessId $backend.Id
    $frontendService.Listener = Wait-AutoInsightListener -Port 5173 -RootProcessId $frontend.Id
    Save-AutoInsightState -State $state

    Write-Host "Backend PID: $($backendService.Listener.ProcessId)  http://localhost:5000"
    Write-Host "Frontend PID: $($frontendService.Listener.ProcessId) http://localhost:5173"
    Write-Host 'Run ./scripts/stop-all.ps1 when finished.'
}
catch {
    Stop-AutoInsightTrackedState -State $state -Quiet
    Remove-AutoInsightState
    throw
}
