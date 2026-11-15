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
$prompts = @(
    'Analyze this dataset, identify missing values, calculate the average ExamScore, detect potential outliers, and generate a chart.',
    'Give me the dataset dimensions, duplicate count, and the three most important data-quality observations.',
    'Calculate descriptive statistics for FinalGrade and explain the mean, median, range, and standard deviation.',
    'Compare the Target categories and generate a categorical frequency chart.',
    'Which numeric columns contain potential outliers? Explain the rule used and do not recommend automatic deletion.'
)

$results = foreach ($prompt in $prompts) {
    $started = Get-Date
    $response = Invoke-RestMethod -Method Post -Uri "$apiUrl/api/agent/chat" -ContentType 'application/json' -Body (@{
        datasetId = $datasetId
        message = $prompt
        mode = 'ai'
    } | ConvertTo-Json)

    $passed = $response.mode -eq 'ai' -and $response.usedExternalProvider -and $response.toolExecutions.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($response.message)
    [pscustomobject]@{
        prompt = $prompt
        passed = $passed
        durationMs = [int]((Get-Date) - $started).TotalMilliseconds
        tools = @($response.toolExecutions | ForEach-Object { $_.tool })
        hasChart = $null -ne $response.chart
        response = $response.message
    }
}

$resultDirectory = Join-Path $projectRoot 'TestResults'
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$resultPath = Join-Path $resultDirectory 'real-ai-evaluation.json'
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
$results | Select-Object prompt, passed, durationMs, @{ Name = 'tools'; Expression = { $_.tools -join ', ' } }, hasChart | Format-Table -AutoSize

if ($results.passed -contains $false) { throw "At least one real AI evaluation failed. Details: $resultPath" }
Write-Host "All five real AI scenarios passed. Detailed results: $resultPath"
