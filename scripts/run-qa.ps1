param(
    [string]$Folder = '.'
)

$scriptPath = Join-Path $PSScriptRoot 'qa.scr'

foreach ($dwg in Get-ChildItem -Path $Folder -Filter '*.dwg') {
    & 'accoreconsole.exe' /i $dwg.FullName /s $scriptPath
    $json = Join-Path $PSScriptRoot ($dwg.BaseName + '.qa.json')
    $csv  = Join-Path $PSScriptRoot ($dwg.BaseName + '.features.csv')
    if (Test-Path $json) { Copy-Item $json -Destination $dwg.Directory -Force }
    if (Test-Path $csv)  { Copy-Item $csv  -Destination $dwg.Directory -Force }
}
