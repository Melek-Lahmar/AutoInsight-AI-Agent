$ErrorActionPreference = 'Stop'
$processHelpers = Join-Path $PSScriptRoot 'process-management.ps1'
. $processHelpers

if (Get-AutoInsightState) {
    throw 'A start-all managed AutoInsight instance is already recorded. Stop it before running this validation.'
}
Assert-AutoInsightPortAvailable -Port 5000
Assert-AutoInsightPortAvailable -Port 5173

Write-Host 'Checking that an unrelated listener is never terminated...'
$sentinel = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 5000)
$sentinel.Start()
try {
    $blockedSafely = $false
    try { & (Join-Path $PSScriptRoot 'start-all.ps1') }
    catch {
        if ($_.Exception.Message -notmatch 'Port 5000 is already in use') { throw }
        $blockedSafely = $true
    }
    if (-not $blockedSafely) { throw 'start-all did not reject an occupied backend port.' }

    $probe = [System.Net.Sockets.TcpClient]::new()
    try {
        $probe.Connect([System.Net.IPAddress]::Loopback, 5000)
        if (-not $probe.Connected) { throw 'The unrelated listener was no longer reachable.' }
    }
    finally { $probe.Dispose() }
}
finally { $sentinel.Stop() }

Write-Host 'Checking tracked startup, health, and shutdown...'
try {
    & (Join-Path $PSScriptRoot 'start-all.ps1')
    $state = Get-AutoInsightState
    if (-not $state -or @($state.Services).Count -ne 2) { throw 'start-all did not record both managed services.' }
    if (@($state.Services | Where-Object { -not $_.Listener }).Count -gt 0) { throw 'A managed listener identity is missing from the process record.' }

    $health = Invoke-RestMethod -Uri 'http://localhost:5000/api/health'
    if ($health.status -ne 'healthy') { throw 'The backend health endpoint did not report healthy.' }
    $frontend = Invoke-WebRequest -Uri 'http://localhost:5173/' -UseBasicParsing
    if ($frontend.StatusCode -ne 200) { throw "The frontend returned HTTP $($frontend.StatusCode)." }

    & (Join-Path $PSScriptRoot 'stop-all.ps1')
    Start-Sleep -Milliseconds 500
    if (Get-AutoInsightState) { throw 'stop-all did not remove the process record.' }
    if (@(Get-AutoInsightPortOwners -Port 5000).Count -gt 0 -or @(Get-AutoInsightPortOwners -Port 5173).Count -gt 0) {
        throw 'A tracked AutoInsight listener remained after stop-all.'
    }
}
finally {
    if (Get-AutoInsightState) { & (Join-Path $PSScriptRoot 'stop-all.ps1') }
}

Write-Host 'Process-management validation passed: occupied ports were preserved, tracked services started successfully, and only tracked processes were stopped.'
