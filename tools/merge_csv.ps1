<#
  merge_csv.ps1  –  rebuild master.csv
  IN : per‑DWG CSVs written by QA_EXPORT_BATCH
  OUT: master dataset for training
#>

$ErrorActionPreference = 'Stop'

$In  = 'C:\Users\Jesse 2025\Desktop\CAD AI\ml\artifacts\Drawings'
$Out = 'C:\Users\Jesse 2025\Desktop\CAD AI\ml\datasets\master.csv'

Write-Host "`nMerging CSVs …"
Write-Host " IN  = $In"
Write-Host " OUT = $Out`n"

if (-not (Test-Path $In)) { throw "Input folder not found: $In" }

$csvs = Get-ChildItem -Path $In -Filter '*.Text.csv'
if ($csvs.Count -eq 0)    { throw "No *.Text.csv files in $In" }

$outDir = Split-Path $Out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

# header once
Get-Content $csvs[0].FullName -First 1 | Set-Content $Out

# append all rows (skip headers)
foreach ($f in $csvs) {
    Get-Content $f.FullName | Select-Object -Skip 1 | Add-Content $Out
}

Write-Host "`u2714 merge complete  →  $Out" -ForegroundColor Green
