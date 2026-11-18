$ErrorActionPreference = 'Stop'
$processHelpers = Join-Path $PSScriptRoot 'process-management.ps1'
. $processHelpers

$state = Get-AutoInsightState
if (-not $state) {
    Write-Host 'No start-all managed AutoInsight instance was found. No process was terminated.'
    return
}

Stop-AutoInsightTrackedState -State $state
Remove-AutoInsightState
