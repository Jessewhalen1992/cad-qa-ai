@echo off
REM -----------------------------------------------------------------
REM  label_dataset.bat
REM  Runs label_with_rules.py to create / refresh labeled.csv
REM -----------------------------------------------------------------
REM Location of master.csv
set "MASTER=ml\datasets\master.csv"

REM Output labeled.csv
set "LABELED=ml\datasets\labeled.csv"

echo Labeling dataset …
echo IN  = %MASTER%
echo OUT = %LABELED%
echo.

python ml\labeling\label_with_rules.py --in %MASTER% --out %LABELED%
if errorlevel 1 (
    echo [ERROR] labeling failed.
) else (
    echo ✓ labeled  →  %LABELED%
)
pause
