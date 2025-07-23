<#
  merge_csv.ps1
  -------------
  Merge every  *.Text.csv  in  $In
  into          $Out
#>

$ErrorActionPreference = 'Stop'   # fail fast

$In  = 'C:\Users\Jesse 2025\Desktop\CAD AI\ml\artifacts\Drawings'
$Out = 'C:\Users\Jesse 2025\Desktop\CAD AI\ml\datasets\master.csv'

Write-Host ""
Write-Host "Merging CSVs..."
Write-Host " IN  = $In"
Write-Host " OUT = $Out"
Write-Host ""

if (-not (Test-Path $In))  { throw "Input folder not found: $In" }
$csvs = Get-ChildItem -Path $In -Filter '*.Text.csv'
if ($csvs.Count -eq 0)     { throw "No *.Text.csv files found in $In" }

$outDir = Split-Path $Out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

# header once
Get-Content $csvs[0] -First 1 | Set-Content $Out

# append all rows, skipping headers
foreach ($f in $csvs) {
    Get-Content $f | Select-Object -Skip 1 | Add-Content $Out
}

Write-Host ""
Write-Host "`u2714 merge complete  →  $Out" -ForegroundColor Green
