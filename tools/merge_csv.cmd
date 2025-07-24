@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -NoExit ^
        -File "%~dp0merge_csv.ps1"
echo.
echo Press any key to close . . .
pause >nul
