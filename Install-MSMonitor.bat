@echo off
echo Microsoft Endpoint Monitor - Quick Installer
echo ===========================================
echo.

REM Check for administrator privileges
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Running with administrator privileges...
    echo.
) else (
    echo ERROR: This installer requires administrator privileges.
    echo Please right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

REM Run PowerShell installer from repository root
echo Starting installer...
powershell.exe -ExecutionPolicy Bypass -File "%~dp0Install-MSMonitor.ps1"

if %errorLevel% == 0 (
    echo.
    echo Installation completed successfully!
) else (
    echo.
    echo Installation failed. Check the log file for details.
)

echo.
pause
