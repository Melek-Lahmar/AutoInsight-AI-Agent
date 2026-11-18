$ErrorActionPreference = 'Stop'

function ConvertTo-InvariantNumber {
    param([Parameter(Mandatory)][string]$Value)
    [double]::Parse($Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Get-NumericSummary {
    param([Parameter(Mandatory)][double[]]$Values)

    $sorted = @($Values | Sort-Object)
    $count = $sorted.Count
    $median = if ($count % 2 -eq 0) { ($sorted[$count / 2 - 1] + $sorted[$count / 2]) / 2 } else { $sorted[[math]::Floor($count / 2)] }
    $mean = ($sorted | Measure-Object -Average).Average
    $variance = ($sorted | ForEach-Object { [math]::Pow($_ - $mean, 2) } | Measure-Object -Sum).Sum / $count
    [pscustomobject]@{
        Count = $count
        Minimum = $sorted[0]
        Maximum = $sorted[-1]
        Mean = $mean
        Median = $median
        StandardDeviation = [math]::Sqrt($variance)
    }
}

function Get-Percentile {
    param(
        [Parameter(Mandatory)][double[]]$Values,
        [Parameter(Mandatory)][double]$Probability
    )

    $sorted = @($Values | Sort-Object)
    $position = ($sorted.Count - 1) * $Probability
    $lower = [math]::Floor($position)
    $upper = [math]::Ceiling($position)
    if ($lower -eq $upper) { return $sorted[$lower] }
    $weight = $position - $lower
    return $sorted[$lower] + (($sorted[$upper] - $sorted[$lower]) * $weight)
}

function Get-MessageNumbers {
    param([Parameter(Mandatory)][string]$Message)

    @([regex]::Matches($Message, '(?<![\p{L}\p{N}_])-?\d+(?:[\.,]\d+)?') | ForEach-Object {
        $candidate = $_.Value.Replace(',', '.')
        $parsed = 0.0
        if ([double]::TryParse($candidate, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
            $parsed
        }
    })
}

function Test-ExpectedNumber {
    param(
        [Parameter(Mandatory)][double[]]$Actual,
        [Parameter(Mandatory)][double]$Expected,
        [double]$Tolerance = 0.01
    )
    @($Actual | Where-Object { [math]::Abs($_ - $Expected) -le $Tolerance }).Count -gt 0
}

function Test-ChartEvidence {
    param(
        $Chart,
        [Parameter(Mandatory)][string]$Rule,
        [Parameter(Mandatory)]$GroundTruth
    )

    if (-not $Chart) { return @('chart response is missing') }
    $failures = @()
    if ($Rule -eq 'exam-score') {
        $identifiesColumn = [string]$Chart.xAxis -eq 'ExamScore' -or [string]$Chart.title -match 'ExamScore'
        if (-not $identifiesColumn) { $failures += 'chart does not identify ExamScore' }
        if (@($Chart.data).Count -eq 0) { $failures += 'chart contains no data points' }
    }
    elseif ($Rule -eq 'target-frequency') {
        if ([string]$Chart.xAxis -ne 'Target') { $failures += "chart X axis is '$($Chart.xAxis)' instead of 'Target'" }
        foreach ($category in $GroundTruth.TargetFrequencies.PSObject.Properties) {
            $point = @($Chart.data | Where-Object { [string]$_.label -eq $category.Name }) | Select-Object -First 1
            if (-not $point -or [double]$point.value -ne [double]$category.Value) {
                $failures += "chart frequency for '$($category.Name)' does not equal $($category.Value)"
            }
        }
    }
    return $failures
}

$apiUrl = if ($env:VITE_API_URL) { $env:VITE_API_URL } else { 'http://localhost:5000' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$samplePath = Join-Path $projectRoot 'sample-data/students_performance.csv'
$rows = @(Import-Csv -LiteralPath $samplePath)

$examSummary = Get-NumericSummary -Values @($rows.ExamScore | Where-Object { $_ } | ForEach-Object { ConvertTo-InvariantNumber $_ })
$gradeSummary = Get-NumericSummary -Values @($rows.FinalGrade | Where-Object { $_ } | ForEach-Object { ConvertTo-InvariantNumber $_ })
$duplicateRows = @($rows | Group-Object { ($_.PSObject.Properties.Value -join [char]31) } | ForEach-Object { [math]::Max(0, $_.Count - 1) } | Measure-Object -Sum).Sum
$missingColumns = [ordered]@{}
foreach ($header in @($rows[0].PSObject.Properties.Name)) {
    $missing = @($rows | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.$header) }).Count
    if ($missing -gt 0) { $missingColumns[$header] = $missing }
}
$targetFrequencies = [ordered]@{}
foreach ($group in @($rows | Group-Object Target)) { $targetFrequencies[$group.Name] = $group.Count }
$outlierColumns = @()
foreach ($header in @('StudyHours', 'Attendance', 'AssignmentScore', 'ExamScore', 'FinalGrade')) {
    $values = [double[]]@($rows.$header | Where-Object { $_ } | ForEach-Object { ConvertTo-InvariantNumber $_ })
    $q1 = Get-Percentile -Values $values -Probability 0.25
    $q3 = Get-Percentile -Values $values -Probability 0.75
    $iqr = $q3 - $q1
    if (@($values | Where-Object { $_ -lt ($q1 - 1.5 * $iqr) -or $_ -gt ($q3 + 1.5 * $iqr) }).Count -gt 0) {
        $outlierColumns += $header
    }
}

$groundTruth = [pscustomobject]@{
    RowCount = $rows.Count
    ColumnCount = @($rows[0].PSObject.Properties).Count
    DuplicateRows = [int]$duplicateRows
    MissingColumns = [pscustomobject]$missingColumns
    ExamScore = $examSummary
    FinalGrade = $gradeSummary
    TargetFrequencies = [pscustomobject]$targetFrequencies
    OutlierColumns = $outlierColumns
}

$config = Invoke-RestMethod -Uri "$apiUrl/api/config"
if (-not $config.aiModeConfigured) {
    throw 'AI Agent Mode is not configured. Add a supported provider, model, and API credential through .NET User Secrets, then restart the backend.'
}
if ([string]::IsNullOrWhiteSpace([string]$config.provider) -or [string]::IsNullOrWhiteSpace([string]$config.model)) {
    throw 'The API reported AI mode as configured without identifying the provider and model.'
}
$maxToolInvocations = if ($config.maxToolInvocations) { [int]$config.maxToolInvocations } else { 8 }

$upload = Invoke-RestMethod -Method Post -Uri "$apiUrl/api/datasets/upload" -Form @{ file = Get-Item -LiteralPath $samplePath }
$datasetId = $upload.overview.id
$scenarios = @(
    [pscustomobject]@{
        prompt = 'Analyze this dataset, identify missing values, calculate the average ExamScore, detect potential outliers, and generate a chart.'
        requiredTools = @('get_missing_values', 'calculate_statistics', 'detect_outliers', 'generate_chart_specification')
        requiredNumbers = @([pscustomobject]@{ label = 'ExamScore mean'; value = $examSummary.Mean; tolerance = 0.01 })
        requiredTerms = @($missingColumns.Keys + $outlierColumns | Select-Object -Unique)
        rangeAlternatives = @()
        chartRule = 'exam-score'
    },
    [pscustomobject]@{
        prompt = 'Give me the dataset dimensions, duplicate count, and the three most important data-quality observations.'
        requiredTools = @('get_dataset_overview', 'get_data_quality')
        requiredNumbers = @(
            [pscustomobject]@{ label = 'row count'; value = $rows.Count; tolerance = 0.001 },
            [pscustomobject]@{ label = 'column count'; value = @($rows[0].PSObject.Properties).Count; tolerance = 0.001 },
            [pscustomobject]@{ label = 'duplicate count'; value = $duplicateRows; tolerance = 0.001 }
        )
        requiredTerms = @()
        rangeAlternatives = @()
        chartRule = $null
    },
    [pscustomobject]@{
        prompt = 'Calculate descriptive statistics for FinalGrade and explain the mean, median, range, and standard deviation.'
        requiredTools = @('calculate_statistics')
        requiredNumbers = @(
            [pscustomobject]@{ label = 'FinalGrade mean'; value = $gradeSummary.Mean; tolerance = 0.01 },
            [pscustomobject]@{ label = 'FinalGrade median'; value = $gradeSummary.Median; tolerance = 0.01 },
            [pscustomobject]@{ label = 'FinalGrade population standard deviation'; value = $gradeSummary.StandardDeviation; tolerance = 0.02 }
        )
        requiredTerms = @()
        rangeAlternatives = @(
            [pscustomobject]@{ label = 'FinalGrade range'; values = @($gradeSummary.Maximum - $gradeSummary.Minimum) },
            [pscustomobject]@{ label = 'FinalGrade minimum and maximum'; values = @($gradeSummary.Minimum, $gradeSummary.Maximum) }
        )
        chartRule = $null
    },
    [pscustomobject]@{
        prompt = 'Compare the Target categories and generate a categorical frequency chart.'
        requiredTools = @('get_categorical_distribution', 'generate_chart_specification')
        requiredNumbers = @()
        requiredTerms = @()
        rangeAlternatives = @()
        chartRule = 'target-frequency'
    },
    [pscustomobject]@{
        prompt = 'Which numeric columns contain potential outliers? Explain the rule used and do not recommend automatic deletion.'
        requiredTools = @('detect_outliers')
        requiredNumbers = @([pscustomobject]@{ label = 'IQR multiplier'; value = 1.5; tolerance = 0.001 })
        requiredTerms = $outlierColumns
        rangeAlternatives = @()
        chartRule = $null
    }
)

$results = @()
foreach ($scenario in $scenarios) {
    $started = Get-Date
    try {
        $response = Invoke-RestMethod -Method Post -Uri "$apiUrl/api/agent/chat" -ContentType 'application/json' -Body (@{
            datasetId = $datasetId
            message = $scenario.prompt
            mode = 'ai'
        } | ConvertTo-Json)

        $tools = @($response.toolExecutions | ForEach-Object { $_.tool })
        $failures = @()
        if ($response.mode -ne 'ai') { $failures += "response mode is '$($response.mode)' instead of 'ai'" }
        if (-not $response.usedExternalProvider) { $failures += 'external-provider use was not confirmed' }
        if ([string]::IsNullOrWhiteSpace([string]$response.message)) { $failures += 'response message is empty' }
        if ($tools.Count -eq 0) { $failures += 'no tool execution was recorded' }
        if ($tools.Count -gt $maxToolInvocations) { $failures += "tool invocation count $($tools.Count) exceeds the configured limit of $maxToolInvocations" }
        foreach ($tool in $scenario.requiredTools) {
            if ($tool -notin $tools) { $failures += "required tool '$tool' was not executed" }
        }
        if (@($response.toolExecutions | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.summary) }).Count -gt 0) {
            $failures += 'a tool execution has no successful completion summary'
        }

        $actualNumbers = [double[]]@(Get-MessageNumbers -Message ([string]$response.message))
        foreach ($expected in $scenario.requiredNumbers) {
            if (-not (Test-ExpectedNumber -Actual $actualNumbers -Expected ([double]$expected.value) -Tolerance ([double]$expected.tolerance))) {
                $failures += "response does not contain the independently calculated $($expected.label) value $([math]::Round([double]$expected.value, 4))"
            }
        }
        if (@($scenario.rangeAlternatives).Count -gt 0) {
            $rangeMatched = $false
            foreach ($alternative in $scenario.rangeAlternatives) {
                $allValuesPresent = @($alternative.values | Where-Object {
                    -not (Test-ExpectedNumber -Actual $actualNumbers -Expected ([double]$_) -Tolerance 0.02)
                }).Count -eq 0
                if ($allValuesPresent) { $rangeMatched = $true; break }
            }
            if (-not $rangeMatched) { $failures += 'response does not contain either the calculated range or its minimum/maximum endpoints' }
        }
        foreach ($term in $scenario.requiredTerms) {
            if ([string]$response.message -notmatch [regex]::Escape([string]$term)) {
                $failures += "response does not identify required column '$term'"
            }
        }
        if ($scenario.chartRule) {
            $failures += @(Test-ChartEvidence -Chart $response.chart -Rule $scenario.chartRule -GroundTruth $groundTruth)
        }

        $results += [pscustomobject]@{
            prompt = $scenario.prompt
            passed = $failures.Count -eq 0
            durationMs = [int]((Get-Date) - $started).TotalMilliseconds
            tools = $tools
            failures = $failures
            chart = $response.chart
            response = $response.message
        }
    }
    catch {
        $results += [pscustomobject]@{
            prompt = $scenario.prompt
            passed = $false
            durationMs = [int]((Get-Date) - $started).TotalMilliseconds
            tools = @()
            failures = @("Provider-backed request failed: $($_.Exception.Message)")
            chart = $null
            response = $null
        }
    }
}

$report = [pscustomobject]@{
    evaluatedAtUtc = [DateTime]::UtcNow.ToString('O')
    provider = $config.provider
    model = $config.model
    timeoutSeconds = $config.timeoutSeconds
    maxToolInvocations = $maxToolInvocations
    sourceDataset = 'sample-data/students_performance.csv'
    groundTruth = $groundTruth
    scenarios = $results
}
$resultDirectory = Join-Path $projectRoot 'TestResults'
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$resultPath = Join-Path $resultDirectory 'real-ai-evaluation.json'
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding utf8
$results | Select-Object prompt, passed, durationMs, @{ Name = 'tools'; Expression = { $_.tools -join ', ' } }, @{ Name = 'failures'; Expression = { $_.failures -join '; ' } } | Format-Table -Wrap -AutoSize

if ($results.passed -contains $false) { throw "At least one real AI evaluation failed. Inspect the local result file: $resultPath" }
Write-Host "All five real AI scenarios passed with provider confirmation, required tool traces, independent numerical ground truth, and structured chart checks. Detailed local results: $resultPath"
