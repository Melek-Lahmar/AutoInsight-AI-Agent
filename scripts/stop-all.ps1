$ErrorActionPreference = 'Stop'
$listeners = Get-NetTCPConnection -State Listen -LocalPort 5000,5173 -ErrorAction SilentlyContinue
$processIds = $listeners | Select-Object -ExpandProperty OwningProcess -Unique
if (-not $processIds) {
    Write-Host 'AutoInsight is not listening on ports 5000 or 5173.'
    return
}
foreach ($processId in $processIds) {
    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped process $processId."
}
