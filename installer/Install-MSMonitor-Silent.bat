@echo off
REM Silent installer for Microsoft Endpoint Monitor
REM For automated deployment scenarios

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: Administrator privileges required
    exit /b 1
)

powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0scripts\Install-MSMonitor.ps1" -Silent
exit /b %errorLevel%
