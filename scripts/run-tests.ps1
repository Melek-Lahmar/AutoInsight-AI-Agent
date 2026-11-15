$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $projectRoot 'AutoInsight.slnx')
Push-Location (Join-Path $projectRoot 'frontend')
try {
    npm test
    npm run lint
    npm run build
}
finally { Pop-Location }
