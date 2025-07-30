<#
full_train.ps1  –  one‑click pipeline
----------------------------------------------------------
 1) Merge   ml\artifacts\Drawings\*.features.csv -> ml\datasets\master.csv
 2) Label   master.csv -> labeled.csv
 3) Train   ml\model.ipynb -> ml\artifacts\layer_clf.pkl
----------------------------------------------------------
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ------ paths -------------------------------------------------------
$root        = $PSScriptRoot           # folder this script is in
$drawingsDir = Join-Path $root 'ml\artifacts\Drawings'
$datasetDir  = Join-Path $root 'ml\datasets'

$masterCsv   = Join-Path $datasetDir 'master.csv'
$labeledCsv  = Join-Path $datasetDir 'labeled.csv'
$labelScript = Join-Path $root 'ml\labeling\label_with_rules.py'
$notebook    = Join-Path $root 'ml\model.ipynb'

# ------ 1. merge ----------------------------------------------------
Write-Host "`n=== MERGING FEATURES => master.csv ===`n"
$csvs = Get-ChildItem -Path $drawingsDir -Filter *.Text.csv -File -Recurse
if (!$csvs) { throw "No *.features.csv found in $drawingsDir" }

$first = $true
foreach ($csv in $csvs) {
    if ($first) {
        Get-Content $csv.FullName | Set-Content $masterCsv
        $first = $false
    } else {
        Get-Content $csv.FullName | Select-Object -Skip 1 | Add-Content $masterCsv
    }
}
Write-Host "master.csv written to $masterCsv"

# ------ 2. label ----------------------------------------------------
Write-Host "`n=== LABELING master.csv => labeled.csv ===`n"
python $labelScript --in $masterCsv --out $labeledCsv
Write-Host "labeled.csv written to $labeledCsv"

# ------ 3. train ----------------------------------------------------
Write-Host "`n=== TRAINING model.ipynb (nbconvert) ===`n"
jupyter nbconvert --to notebook --execute "`"$notebook`"" --inplace
Write-Host "layer_clf.pkl saved to ml\\artifacts\\layer_clf.pkl"

# ------ done --------------------------------------------------------
Write-Host ''
Write-Host 'All steps complete.'
Write-Host 'Next in AutoCAD:'
Write-Host '  - NETLOAD  (load / reload CadQaPlugin.dll)'
Write-Host '  - RUNQAAUDIT   or   RUNQAAUDITSEL'
Write-Host ''
pause
