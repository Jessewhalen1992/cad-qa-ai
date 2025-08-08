<#
full_train.ps1  –  one‑click pipeline
----------------------------------------------------------
 1) Merge   ml\artifacts\Drawings\*.Text.csv -> ml\datasets\master.csv
 2) Label   master.csv -> labeled.csv
 3) Train   layer_model_training.ipynb -> ml\artifacts\layer_clf.pkl
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

# ------ 1. merge ----------------------------------------------------
Write-Host "`n=== MERGING FEATURES => master.csv ===`n"
$csvs = Get-ChildItem -Path $drawingsDir -Filter *.Text.csv -File -Recurse
if (!$csvs) { throw "No *.Text.csv found in $drawingsDir" }

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

# ------ 2.5 generate allowed tokens ---------------------------------
Write-Host "`n=== Generating allowed tokens list ===`n"
python ml\generate_allowed_tokens.py --in $masterCsv --out spell_allowed_tokens.txt --min_count 10
Write-Host "spell_allowed_tokens.txt generated/updated"


# ------ 3. train ----------------------------------------------------
Write-Host "=== TRAINING layer_model_training.ipynb ==="
$jupyter = "jupyter"

# execute notebook -> artifacts\model_run.ipynb
& $jupyter nbconvert --to notebook --execute `
    ".\layer_model_training.ipynb" `
    --output "ml\artifacts\model_run.ipynb" `
    --log-level=ERROR
if ($LASTEXITCODE -ne 0) {
    Write-Error "nbconvert failed – training aborted."
    pause; exit 1
}

# verify the generated model
$model = "ml\artifacts\layer_clf.pkl"
if (-not (Test-Path $model)) {
    Write-Error "layer_clf.pkl not produced."
    pause; exit 1
}

# quick Python guard – size + fitted flag
python ml\verify_layer_model.py
if ($LASTEXITCODE -ne 0) {
    Write-Error "Model verification failed – fix notebook."
    pause; exit 1
}

# ------ done --------------------------------------------------------
Write-Host ''
Write-Host 'All steps complete.'
Write-Host 'Next in AutoCAD:'
Write-Host '  - NETLOAD  (load / reload CadQaPlugin.dll)'
Write-Host '  - RUNQAAUDIT   or   RUNQAAUDITSEL'
Write-Host ''
pause
