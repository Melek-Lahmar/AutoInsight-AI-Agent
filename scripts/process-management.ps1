$script:AutoInsightStateDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) '.autoinsight'
$script:AutoInsightStatePath = Join-Path $script:AutoInsightStateDirectory 'processes.json'

function Get-AutoInsightProcessIdentity {
    param([Parameter(Mandatory)][int]$ProcessId)

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $process) { return $null }

    [pscustomobject]@{
        ProcessId = $process.Id
        Name = $process.ProcessName
        StartedAtUtc = $process.StartTime.ToUniversalTime().ToString('O')
        StartedAtUtcTicks = $process.StartTime.ToUniversalTime().Ticks
    }
}

function Test-AutoInsightProcessIdentity {
    param([Parameter(Mandatory)]$Identity)

    $current = Get-AutoInsightProcessIdentity -ProcessId ([int]$Identity.ProcessId)
    if (-not $current) { return $false }
    if ($Identity.PSObject.Properties.Name -contains 'StartedAtUtcTicks') {
        return $current.StartedAtUtcTicks -eq [long]$Identity.StartedAtUtcTicks
    }
    $recordedStart = [DateTimeOffset]$Identity.StartedAtUtc
    return $current.StartedAtUtcTicks -eq $recordedStart.UtcDateTime.Ticks
}

function Get-AutoInsightState {
    if (-not (Test-Path -LiteralPath $script:AutoInsightStatePath)) { return $null }
    try {
        return Get-Content -LiteralPath $script:AutoInsightStatePath -Raw | ConvertFrom-Json
    }
    catch {
        throw "The AutoInsight process record is unreadable: $script:AutoInsightStatePath. Remove this file only after confirming that no AutoInsight process is running."
    }
}

function Save-AutoInsightState {
    param([Parameter(Mandatory)]$State)

    New-Item -ItemType Directory -Path $script:AutoInsightStateDirectory -Force | Out-Null
    $State | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $script:AutoInsightStatePath -Encoding utf8
}

function Remove-AutoInsightState {
    if (Test-Path -LiteralPath $script:AutoInsightStatePath) {
        Remove-Item -LiteralPath $script:AutoInsightStatePath -Force
    }
}

function Get-AutoInsightPortOwners {
    param([Parameter(Mandatory)][int]$Port)

    @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
}

function Assert-AutoInsightPortAvailable {
    param([Parameter(Mandatory)][int]$Port)

    $owners = @(Get-AutoInsightPortOwners -Port $Port)
    if ($owners.Count -eq 0) { return }

    $descriptions = foreach ($ownerId in $owners) {
        $owner = Get-Process -Id $ownerId -ErrorAction SilentlyContinue
        if ($owner) { "PID $ownerId ($($owner.ProcessName))" } else { "PID $ownerId" }
    }
    throw "Port $Port is already in use by $($descriptions -join ', '). AutoInsight did not start and will not terminate that process."
}

function Test-AutoInsightDescendant {
    param(
        [Parameter(Mandatory)][int]$ProcessId,
        [Parameter(Mandatory)][int]$AncestorProcessId
    )

    if ($ProcessId -eq $AncestorProcessId) { return $true }
    $visited = [System.Collections.Generic.HashSet[int]]::new()
    $currentId = $ProcessId
    while ($currentId -gt 0 -and $visited.Add($currentId)) {
        $process = Get-CimInstance Win32_Process -Filter "ProcessId = $currentId" -ErrorAction SilentlyContinue
        if (-not $process) { return $false }
        $currentId = [int]$process.ParentProcessId
        if ($currentId -eq $AncestorProcessId) { return $true }
    }
    return $false
}

function Wait-AutoInsightListener {
    param(
        [Parameter(Mandatory)][int]$Port,
        [Parameter(Mandatory)][int]$RootProcessId,
        [int]$TimeoutSeconds = 45
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        foreach ($ownerId in @(Get-AutoInsightPortOwners -Port $Port)) {
            if (Test-AutoInsightDescendant -ProcessId $ownerId -AncestorProcessId $RootProcessId) {
                return Get-AutoInsightProcessIdentity -ProcessId $ownerId
            }
            throw "Port $Port was claimed by unrelated PID $ownerId while AutoInsight was starting. The tracked AutoInsight process will be cleaned up; the unrelated process will not be stopped."
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "AutoInsight did not begin listening on port $Port within $TimeoutSeconds seconds."
}

function Get-AutoInsightDescendantIds {
    param([Parameter(Mandatory)][int]$RootProcessId)

    $processes = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
    $owned = [System.Collections.Generic.HashSet[int]]::new()
    [void]$owned.Add($RootProcessId)
    $changed = $true
    while ($changed) {
        $changed = $false
        foreach ($process in $processes) {
            if ($owned.Contains([int]$process.ParentProcessId) -and $owned.Add([int]$process.ProcessId)) {
                $changed = $true
            }
        }
    }
    @($owned)
}

function Stop-AutoInsightTrackedState {
    param(
        [Parameter(Mandatory)]$State,
        [switch]$Quiet
    )

    $ownedIds = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($service in @($State.Services)) {
        if ($service.Root -and (Test-AutoInsightProcessIdentity -Identity $service.Root)) {
            foreach ($ownedId in @(Get-AutoInsightDescendantIds -RootProcessId ([int]$service.Root.ProcessId))) {
                [void]$ownedIds.Add($ownedId)
            }
        }
        if ($service.Listener -and (Test-AutoInsightProcessIdentity -Identity $service.Listener)) {
            [void]$ownedIds.Add([int]$service.Listener.ProcessId)
        }
    }

    if ($ownedIds.Count -eq 0) {
        if (-not $Quiet) { Write-Host 'The AutoInsight process record was stale; no process was terminated.' }
        return
    }

    $listenerIds = @($State.Services | ForEach-Object { if ($_.Listener) { [int]$_.Listener.ProcessId } })
    $orderedIds = @($listenerIds + @($ownedIds)) | Select-Object -Unique
    foreach ($ownedId in $orderedIds) {
        if ($ownedIds.Contains([int]$ownedId) -and (Get-Process -Id $ownedId -ErrorAction SilentlyContinue)) {
            Stop-Process -Id $ownedId -Force -ErrorAction SilentlyContinue
            if (-not $Quiet) { Write-Host "Stopped tracked AutoInsight process $ownedId." }
        }
    }
}
