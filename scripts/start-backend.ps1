$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $projectRoot 'backend/AutoInsight.Api/AutoInsight.Api.csproj') --urls http://localhost:5000
