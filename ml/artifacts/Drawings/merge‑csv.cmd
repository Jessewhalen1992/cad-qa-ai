@echo off
REM ---------------------------------------------
REM Wrapper that keeps the window open so you can
REM read any success or error messages
REM ---------------------------------------------
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0merge_csv.ps1"
echo.
echo Press any key to close this window . . .
pause >nul
