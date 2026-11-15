# API examples

Upload a sample dataset:

```powershell
$upload = Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/datasets/upload -Form @{
    file = Get-Item ./sample-data/students_performance.csv
}
$datasetId = $upload.overview.id
```

Run deterministic Demo Mode:

```powershell
$body = @{
    datasetId = $datasetId
    message = 'Identify quality issues, calculate useful statistics, and chart FinalGrade.'
    mode = 'demo'
} | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/agent/chat -ContentType application/json -Body $body
```

Generate a histogram:

```powershell
$chart = @{ type = 'histogram'; xColumn = 'FinalGrade'; bins = 10 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/datasets/$datasetId/visualizations" -ContentType application/json -Body $chart
```
