$ErrorActionPreference = 'Stop'
$In  = 'C:\Users\Jesse 2025\Desktop\CAD AI\ml\artifacts\Drawings'
$Out = 'C:\Users\Jesse 2025\Desktop\CAD AI\ml\datasets\master.csv'

Write-Host "`nMerging CSVs ..."
if (-not (Test-Path $In))  { throw "Input folder not found: $In" }
$csvs = Get-ChildItem -Path $In -Filter '*.Text.csv'
if ($csvs.Count -eq 0)     { throw "No *.Text.csv files in $In" }

$outDir = Split-Path $Out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

Get-Content $csvs[0] -First 1 | Set-Content $Out
foreach ($f in $csvs) { Get-Content $f | Select-Object -Skip 1 | Add-Content $Out }

Write-Host "`u2714 merge complete  →  $Out" -ForegroundColor Green
