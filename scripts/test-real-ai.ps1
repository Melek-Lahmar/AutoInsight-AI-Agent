$ErrorActionPreference = 'Stop'

$apiUrl = if ($env:VITE_API_URL) { $env:VITE_API_URL } else { 'http://localhost:5000' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$samplePath = Join-Path $projectRoot 'sample-data/students_performance.csv'

$config = Invoke-RestMethod -Uri "$apiUrl/api/config"
if (-not $config.aiModeConfigured) {
    throw 'AI Agent Mode is not configured. Add a supported provider, model, and API credential through .NET User Secrets, then restart the backend.'
}

$upload = Invoke-RestMethod -Method Post -Uri "$apiUrl/api/datasets/upload" -Form @{ file = Get-Item -LiteralPath $samplePath }
$datasetId = $upload.overview.id
$scenarios = @(
    [pscustomobject]@{
        prompt = 'Analyze this dataset, identify missing values, calculate the average ExamScore, detect potential outliers, and generate a chart.'
        requiredTools = @('get_missing_values', 'calculate_statistics', 'detect_outliers', 'generate_chart_specification')
        requiredPatterns = @('79[\.,]1(?:0)?', 'missing', 'outlier')
        chartRequired = $true
    },
    [pscustomobject]@{
        prompt = 'Give me the dataset dimensions, duplicate count, and the three most important data-quality observations.'
        requiredTools = @('get_dataset_overview', 'get_data_quality')
        requiredPatterns = @('\b20\b', '\b7\b', 'duplicate')
        chartRequired = $false
    },
    [pscustomobject]@{
        prompt = 'Calculate descriptive statistics for FinalGrade and explain the mean, median, range, and standard deviation.'
        requiredTools = @('calculate_statistics')
        requiredPatterns = @('78[\.,]86', '83[\.,]5(?:0)?', 'standard deviation')
        chartRequired = $false
    },
    [pscustomobject]@{
        prompt = 'Compare the Target categories and generate a categorical frequency chart.'
        requiredTools = @('get_categorical_distribution', 'generate_chart_specification')
        requiredPatterns = @('Pass', 'Review', '\b15\b', '\b5\b')
        chartRequired = $true
    },
    [pscustomobject]@{
        prompt = 'Which numeric columns contain potential outliers? Explain the rule used and do not recommend automatic deletion.'
        requiredTools = @('detect_outliers')
        requiredPatterns = @('IQR|interquartile', '1[\.,]5', 'StudyHours')
        chartRequired = $false
    }
)

$results = foreach ($scenario in $scenarios) {
    $started = Get-Date
    $response = Invoke-RestMethod -Method Post -Uri "$apiUrl/api/agent/chat" -ContentType 'application/json' -Body (@{
        datasetId = $datasetId
        message = $scenario.prompt
        mode = 'ai'
    } | ConvertTo-Json)

    $tools = @($response.toolExecutions | ForEach-Object { $_.tool })
    $missingTools = @($scenario.requiredTools | Where-Object { $_ -notin $tools })
    $missingPatterns = @($scenario.requiredPatterns | Where-Object { $response.message -notmatch $_ })
    $baseChecks = $response.mode -eq 'ai' -and $response.usedExternalProvider -and $tools.Count -gt 0 -and $tools.Count -le 8 -and -not [string]::IsNullOrWhiteSpace($response.message)
    $chartCheck = -not $scenario.chartRequired -or $null -ne $response.chart
    $passed = $baseChecks -and $chartCheck -and $missingTools.Count -eq 0 -and $missingPatterns.Count -eq 0

    [pscustomobject]@{
        prompt = $scenario.prompt
        passed = $passed
        durationMs = [int]((Get-Date) - $started).TotalMilliseconds
        tools = $tools
        missingTools = $missingTools
        missingEvidencePatterns = $missingPatterns
        hasChart = $null -ne $response.chart
        response = $response.message
    }
}

$resultDirectory = Join-Path $projectRoot 'TestResults'
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$resultPath = Join-Path $resultDirectory 'real-ai-evaluation.json'
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$results | Select-Object prompt, passed, durationMs, @{ Name = 'tools'; Expression = { $_.tools -join ', ' } }, hasChart | Format-Table -AutoSize

if ($results.passed -contains $false) { throw "At least one real AI evaluation failed. Inspect the local result file: $resultPath" }
Write-Host "All five real AI scenarios passed with required tool traces and numerical evidence. Detailed local results: $resultPath"
